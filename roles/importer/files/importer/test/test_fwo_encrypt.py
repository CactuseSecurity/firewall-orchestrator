from __future__ import annotations

import base64
import hashlib
import hmac
import os

import pytest
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from fwo_encrypt import (
    AUTHENTICATED_PREFIX,
    ENCRYPTION_KEY_LABEL,
    MAC_KEY_LABEL,
    TAG_LENGTH,
    decrypt,
    decrypt_aes_ciphertext,
)

k_main_key = "0123456789ABCDEF0123456789ABCDEF"
k_other_key = "FEDCBA9876543210FEDCBA9876543210"
k_vector_plaintext = "pw:with:colon äöü€"
# k_vector_plaintext encrypted with k_main_key and IV 00..0f, shared with the C# test AesEncryptionTest.cs:
# C# and Python must read each other's secrets
k_vector_ciphertext = "fwo-v2:AAECAwQFBgcICQoLDA0OD1ufs4AnvnYj6EC6iJI2Uy3RsaYUWNvBkQ7DpC0hylIZYIegs7IKHHLf45uJxN/AnTf5hrKcTwy/SDPHrST8ET0="
# "pw:with:colon" encrypted with k_other_key in the legacy format (AES-CBC without tag) and IV 00..0f
k_legacy_ciphertext = "AAECAwQFBgcICQoLDA0ODxFcDA027hBvCxTC/0FVQ/M="
k_wrong_key_attempts = 500


def encrypt_authenticated(plaintext: str, main_key: str, iv: bytes) -> str:
    """Encrypt like FWO.Encryption.AesEnc.Encrypt (C#), which writes all stored secrets."""
    key = main_key.encode()
    encryption_key = hmac.new(key, ENCRYPTION_KEY_LABEL, hashlib.sha256).digest()
    mac_key = hmac.new(key, MAC_KEY_LABEL, hashlib.sha256).digest()
    padder = padding.PKCS7(128).padder()
    padded = padder.update(plaintext.encode()) + padder.finalize()
    encryptor = Cipher(algorithms.AES(encryption_key), modes.CBC(iv)).encryptor()
    authenticated_data = iv + encryptor.update(padded) + encryptor.finalize()
    tag = hmac.new(mac_key, authenticated_data, hashlib.sha256).digest()
    return AUTHENTICATED_PREFIX + base64.b64encode(authenticated_data + tag).decode()


def flip_byte(ciphertext: str, index: int) -> str:
    payload = bytearray(base64.b64decode(ciphertext[len(AUTHENTICATED_PREFIX) :]))
    payload[index] ^= 0x01
    return AUTHENTICATED_PREFIX + base64.b64encode(bytes(payload)).decode()


def test_decrypts_cross_language_vector() -> None:
    assert encrypt_authenticated(k_vector_plaintext, k_main_key, bytes(range(16))) == k_vector_ciphertext
    assert decrypt_aes_ciphertext(k_vector_ciphertext, k_main_key) == k_vector_plaintext


def test_decrypts_base64_with_line_breaks() -> None:
    # PostgreSQL encode(..., 'base64') wraps lines after 76 characters
    wrapped = k_vector_ciphertext[:50] + "\n" + k_vector_ciphertext[50:]
    assert decrypt_aes_ciphertext(wrapped, k_main_key) == k_vector_plaintext


def test_decrypts_legacy_format() -> None:
    assert decrypt_aes_ciphertext(k_legacy_ciphertext, k_other_key) == "pw:with:colon"


def test_always_rejects_wrong_key() -> None:
    # the legacy format accepted about 1 in 250 wrong keys through a valid padding by chance (#5476)
    for _ in range(k_wrong_key_attempts):
        ciphertext = encrypt_authenticated("secret", k_other_key, os.urandom(16))
        with pytest.raises(ValueError, match="tag mismatch"):
            decrypt_aes_ciphertext(ciphertext, k_main_key)


@pytest.mark.parametrize("index", [0, -TAG_LENGTH - 1, -1])
def test_rejects_modified_iv_ciphertext_or_tag(index: int) -> None:
    with pytest.raises(ValueError, match="tag mismatch"):
        decrypt_aes_ciphertext(flip_byte(k_vector_ciphertext, index), k_main_key)


@pytest.mark.parametrize(
    "ciphertext",
    [
        AUTHENTICATED_PREFIX,
        AUTHENTICATED_PREFIX + base64.b64encode(bytes(16 + TAG_LENGTH)).decode(),
        AUTHENTICATED_PREFIX + base64.b64encode(bytes(17 + 16 + TAG_LENGTH)).decode(),
        AUTHENTICATED_PREFIX + "abc",
    ],
)
def test_rejects_invalid_authenticated_payload(ciphertext: str) -> None:
    with pytest.raises(ValueError, match="AES decryption failed"):
        decrypt_aes_ciphertext(ciphertext, k_main_key)


def test_never_falls_back_to_legacy_format() -> None:
    with pytest.raises(ValueError, match="invalid ciphertext length"):
        decrypt_aes_ciphertext(AUTHENTICATED_PREFIX + k_legacy_ciphertext, k_other_key)


def test_rejects_invalid_main_key_length() -> None:
    with pytest.raises(ValueError, match="16, 24 or 32 bytes"):
        decrypt_aes_ciphertext(k_vector_ciphertext, "short")


def test_decrypt_wrapper_raises_for_authenticated_secret_with_wrong_key() -> None:
    # an authenticated secret is certainly encrypted, so its ciphertext must never be used as the secret
    with pytest.raises(ValueError, match="tag mismatch"):
        decrypt(k_vector_ciphertext, k_other_key)


def test_decrypt_wrapper_passes_on_undecryptable_legacy_value() -> None:
    assert decrypt("plain-secret", k_main_key) == "plain-secret"
    assert decrypt(k_vector_ciphertext, k_main_key) == k_vector_plaintext
