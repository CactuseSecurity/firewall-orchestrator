# a standalone script copied to the test system, not part of a package
# ruff: noqa: INP001
"""
Name server for the app server ip change integration test.

Answers the reverse lookups (PTR) of the test ips with the test name and forwards every other query to the
name server configured in /etc/resolv.conf. The app data import resolves app server names with real PTR
queries, so the test needs a name server that knows its ips. Only the test run started by
run_with_test_dns.sh uses it, the resolver configuration of the system stays as it is.
"""

from __future__ import annotations

import argparse
import ipaddress
import socket
import struct
import time
from pathlib import Path

DNS_PORT = 53
HEADER_LENGTH = 12
MAX_MESSAGE_SIZE = 4096
PTR_TYPE = 12
INTERNET_CLASS = 1
RESPONSE_FLAG = 0x8000
AUTHORITATIVE_FLAG = 0x0400
RECURSION_DESIRED_FLAG = 0x0100
RECURSION_AVAILABLE_FLAG = 0x0080
OPCODE_MASK = 0x7800
SERVER_FAILURE = 2
NAME_POINTER_TO_QUESTION = 0xC00C
ANSWER_TTL_SECONDS = 0
FORWARD_TIMEOUT_SECONDS = 5.0
RECEIVE_TIMEOUT_SECONDS = 1.0
RESOLV_CONF = Path("/etc/resolv.conf")
NAME_SERVER_LINE_FIELDS = 2


def parse_ptr_records(ptr_arguments: list[str]) -> dict[str, str]:
    """Map the reverse names of the given ips (format ip=name) to their names."""
    records: dict[str, str] = {}
    for ptr_argument in ptr_arguments:
        ip, name = ptr_argument.split("=", 1)
        records[ipaddress.ip_address(ip).reverse_pointer.lower()] = name
    return records


def read_upstream_name_server(listen_address: str) -> str | None:
    """Return the first name server of the system that is not this one."""
    for line in RESOLV_CONF.read_text(encoding="utf-8").splitlines():
        fields = line.split()
        if len(fields) >= NAME_SERVER_LINE_FIELDS and fields[0] == "nameserver" and fields[1] != listen_address:
            return fields[1]
    return None


def read_question(query: bytes) -> tuple[str, int, int]:
    """Return the name, the type and the end offset of the single question of a query."""
    labels: list[str] = []
    offset = HEADER_LENGTH
    length = query[offset]
    while length != 0:
        labels.append(query[offset + 1 : offset + 1 + length].decode("ascii"))
        offset += 1 + length
        length = query[offset]
    question_type = struct.unpack("!H", query[offset + 1 : offset + 3])[0]
    question_end = offset + 5
    return ".".join(labels).lower(), question_type, question_end


def encode_name(name: str) -> bytes:
    """Encode a domain name as a sequence of labels."""
    encoded = b""
    for label in name.rstrip(".").split("."):
        encoded += bytes([len(label)]) + label.encode("ascii")
    return encoded + b"\x00"


def build_ptr_answer(query: bytes, question_end: int, target_name: str) -> bytes:
    """Build the authoritative answer to a PTR query."""
    query_id, query_flags = struct.unpack("!HH", query[:4])
    flags = (
        RESPONSE_FLAG
        | (query_flags & OPCODE_MASK)
        | AUTHORITATIVE_FLAG
        | (query_flags & RECURSION_DESIRED_FLAG)
        | RECURSION_AVAILABLE_FLAG
    )
    header = struct.pack("!HHHHHH", query_id, flags, 1, 1, 0, 0)
    question = query[HEADER_LENGTH:question_end]
    target = encode_name(target_name)
    answer = struct.pack("!HHHIH", NAME_POINTER_TO_QUESTION, PTR_TYPE, INTERNET_CLASS, ANSWER_TTL_SECONDS, len(target))
    return header + question + answer + target


def build_server_failure(query: bytes) -> bytes:
    """Build an answer telling the client that the query could not be answered."""
    query_id, query_flags = struct.unpack("!HH", query[:4])
    flags = RESPONSE_FLAG | (query_flags & OPCODE_MASK) | (query_flags & RECURSION_DESIRED_FLAG) | SERVER_FAILURE
    return struct.pack("!HHHHHH", query_id, flags, 0, 0, 0, 0)


def forward_query(query: bytes, upstream: str | None) -> bytes:
    """Forward a query to the upstream name server and return its answer."""
    if upstream is None:
        return build_server_failure(query)
    family = socket.AF_INET6 if ":" in upstream else socket.AF_INET
    with socket.socket(family, socket.SOCK_DGRAM) as upstream_socket:
        upstream_socket.settimeout(FORWARD_TIMEOUT_SECONDS)
        try:
            upstream_socket.sendto(query, (upstream, DNS_PORT))
            answer, _ = upstream_socket.recvfrom(MAX_MESSAGE_SIZE)
        except OSError:
            return build_server_failure(query)
    return answer


def answer_query(query: bytes, records: dict[str, str], upstream: str | None) -> bytes:
    """Answer a PTR query of a test ip, forward all other queries."""
    try:
        name, question_type, question_end = read_question(query)
    except (IndexError, UnicodeDecodeError, struct.error):
        return forward_query(query, upstream)
    if question_type == PTR_TYPE and name in records:
        return build_ptr_answer(query, question_end, records[name])
    return forward_query(query, upstream)


def serve(listen_address: str, records: dict[str, str], ready_file: Path, max_seconds: float) -> None:
    """Answer queries until stopped, at most for the given time."""
    upstream = read_upstream_name_server(listen_address)
    end_time = time.monotonic() + max_seconds
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server_socket:
        server_socket.bind((listen_address, DNS_PORT))
        server_socket.settimeout(RECEIVE_TIMEOUT_SECONDS)
        ready_file.write_text("ready\n", encoding="utf-8")
        while time.monotonic() < end_time:
            try:
                query, client = server_socket.recvfrom(MAX_MESSAGE_SIZE)
            # python 3.9 (Red Hat 9) raises socket.timeout, which only became an alias of TimeoutError in 3.10
            except socket.timeout:  # noqa: UP041
                continue
            server_socket.sendto(answer_query(query, records, upstream), client)


def main() -> None:
    parser = argparse.ArgumentParser(description="answer the PTR records of the app server ip change test")
    parser.add_argument("--listen", required=True, help="loopback address to listen on, port 53")
    parser.add_argument("--ptr", action="append", required=True, help="ip=name, may be repeated")
    parser.add_argument("--ready-file", required=True, type=Path, help="created when queries are answered")
    parser.add_argument("--max-seconds", type=float, default=1800.0, help="stop after this time at the latest")
    args = parser.parse_args()
    serve(args.listen, parse_ptr_records(args.ptr), args.ready_file, args.max_seconds)


if __name__ == "__main__":
    main()
