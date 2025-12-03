import base64
import traceback
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
from fwo_log import FWOLogger
from fwo_const import MAIN_KEY_FILE

# can be used for decrypting text encrypted with C# (mw-server)
def decrypt_aes_ciphertext(base64_encrypted_text: str, passphrase: str) -> str:
    encrypted_data = base64.b64decode(base64_encrypted_text)
    iv_length = 16 # IV length for AES is 16 bytes

    # Extract IV from the encrypted data
    iv = encrypted_data[:iv_length]  

    # Initialize AES cipher with provided passphrase and IV
    backend = default_backend()
    cipher = Cipher(algorithms.AES(passphrase.encode()), modes.CBC(iv), backend=backend)
    decryptor = cipher.decryptor()

    # Decrypt the ciphertext
    decrypted_data = decryptor.update(encrypted_data[iv_length:]) + decryptor.finalize()

    # Remove padding
    unpadder = padding.PKCS7(algorithms.AES.block_size).unpadder() #TODO: Check if block_size is correct #type: ignore
    try:
        unpadded_data = unpadder.update(decrypted_data) + unpadder.finalize()
        return unpadded_data.decode('utf-8')  # Assuming plaintext is UTF-8 encoded
    except ValueError as e:
        raise ValueError('AES decryption failed:', e)


# wrapper for trying the different decryption methods
def decrypt(encrypted_data: str, passphrase: str) -> str:
    try:
        decrypted = decrypt_aes_ciphertext(encrypted_data, passphrase)
        return decrypted
    except Exception:
        FWOLogger.warning("Unspecified error while decrypting with AES: " + str(traceback.format_exc()))
        return encrypted_data


def read_main_key(file_path: str = MAIN_KEY_FILE) -> str:
    with open(file_path, "r") as keyfile:
        main_key = keyfile.read().rstrip(' \n')
    return main_key
