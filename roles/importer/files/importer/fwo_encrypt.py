import base64
import binascii
import hashlib
import hmac
import traceback

from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from fwo_const import MAIN_KEY_FILE
from fwo_log import FWOLogger

# Secrets are stored in the authenticated format "fwo-v2:" + base64(IV | AES-256-CBC ciphertext | HMAC-SHA256 tag)
# (encrypt-then-MAC, keys derived from the main key). The tag lets decryption with a wrong main key fail reliably.
# The legacy format (base64 of IV | AES-CBC ciphertext with the main key itself) can still be read.
# Same format as FWO.Encryption.AesEnc (C#) and fworch-encryption.sql (database).
AUTHENTICATED_PREFIX = "fwo-v2:"
IV_LENGTH = 16
BLOCK_LENGTH = 16
TAG_LENGTH = 32
ENCRYPTION_KEY_LABEL = b"fwo-secret-encryption-v2"
AUTHENTICATION_KEY_LABEL = b"fwo-secret-authentication-v2"
VALID_MAIN_KEY_LENGTHS = (16, 24, 32)


def derive_key(main_key: bytes, label: bytes) -> bytes:
    return hmac.new(main_key, label, hashlib.sha256).digest()


def get_main_key_bytes(passphrase: str) -> bytes:
    main_key = passphrase.encode("utf-8")
    if len(main_key) not in VALID_MAIN_KEY_LENGTHS:
        raise ValueError("AES decryption failed: the main key must be 16, 24 or 32 bytes long")
    return main_key


def decrypt_cbc(ciphertext: bytes, key: bytes, iv: bytes) -> bytes:
    """Decrypt AES-CBC and remove the PKCS7 padding, raising ValueError for an invalid padding."""
    decryptor = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend()).decryptor()
    decrypted_data = decryptor.update(ciphertext) + decryptor.finalize()
    unpadder = padding.PKCS7(algorithms.AES.block_size).unpadder()  # type: ignore  # noqa: PGH003
    return unpadder.update(decrypted_data) + unpadder.finalize()


def decrypt_authenticated_ciphertext(base64_payload: str, passphrase: str) -> str:
    main_key = get_main_key_bytes(passphrase)
    try:
        payload = base64.b64decode(base64_payload)
    except binascii.Error as e:
        raise ValueError("AES decryption failed: invalid base64") from e

    ciphertext_length = len(payload) - IV_LENGTH - TAG_LENGTH
    if ciphertext_length < BLOCK_LENGTH or ciphertext_length % BLOCK_LENGTH != 0:
        raise ValueError("AES decryption failed: invalid ciphertext length")

    # verify the tag before decrypting anything: a wrong main key or modified data fails here
    authenticated_data = payload[: IV_LENGTH + ciphertext_length]
    expected_tag = hmac.new(derive_key(main_key, AUTHENTICATION_KEY_LABEL), authenticated_data, hashlib.sha256).digest()
    if not hmac.compare_digest(expected_tag, payload[IV_LENGTH + ciphertext_length :]):
        raise ValueError("AES decryption failed: authentication tag mismatch")

    iv = payload[:IV_LENGTH]
    plaintext = decrypt_cbc(authenticated_data[IV_LENGTH:], derive_key(main_key, ENCRYPTION_KEY_LABEL), iv)
    return plaintext.decode("utf-8")


def decrypt_legacy_ciphertext(base64_encrypted_text: str, passphrase: str) -> str:
    encrypted_data = base64.b64decode(base64_encrypted_text)
    iv = encrypted_data[:IV_LENGTH]
    try:
        # a wrong main key is only detected by an invalid padding (or invalid UTF-8)
        unpadded_data = decrypt_cbc(encrypted_data[IV_LENGTH:], passphrase.encode(), iv)
        return unpadded_data.decode("utf-8")  # Assuming plaintext is UTF-8 encoded
    except ValueError as e:
        raise ValueError("AES decryption failed:", e)


# can be used for decrypting text encrypted with C# (mw-server)
def decrypt_aes_ciphertext(base64_encrypted_text: str, passphrase: str) -> str:
    if base64_encrypted_text.startswith(AUTHENTICATED_PREFIX):
        return decrypt_authenticated_ciphertext(base64_encrypted_text[len(AUTHENTICATED_PREFIX) :], passphrase)
    return decrypt_legacy_ciphertext(base64_encrypted_text, passphrase)


# wrapper for trying the different decryption methods
# a secret that fails in the legacy format is passed on unchanged (it may not be encrypted at all), whereas one in the
# authenticated format is certainly encrypted, so the error is raised instead of using the ciphertext as secret
def decrypt(encrypted_data: str, passphrase: str) -> str:
    if encrypted_data.startswith(AUTHENTICATED_PREFIX):
        return decrypt_aes_ciphertext(encrypted_data, passphrase)
    try:
        return decrypt_aes_ciphertext(encrypted_data, passphrase)
    except Exception:
        FWOLogger.warning("Unspecified error while decrypting with AES: " + str(traceback.format_exc()))
        return encrypted_data


def read_main_key(file_path: str = MAIN_KEY_FILE) -> str:
    with open(file_path) as keyfile:
        return keyfile.read().rstrip(" \n")
