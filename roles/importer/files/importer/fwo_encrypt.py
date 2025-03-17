import base64
import traceback
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
import traceback
from fwo_log import getFwoLogger

# can be used for decrypting text encrypted with C# (mw-server)
def decrypt_aes_ciphertext(base64_encrypted_text, passphrase):
    encrypted_data = base64.b64decode(base64_encrypted_text)
    ivLength = 16 # IV length for AES is 16 bytes

    # Extract IV from the encrypted data
    iv = encrypted_data[:ivLength]  

    # Initialize AES cipher with provided passphrase and IV
    backend = default_backend()
    cipher = Cipher(algorithms.AES(passphrase.encode()), modes.CBC(iv), backend=backend)
    decryptor = cipher.decryptor()

    # Decrypt the ciphertext
    decrypted_data = decryptor.update(encrypted_data[ivLength:]) + decryptor.finalize()

    # Remove padding
    unpadder = padding.PKCS7(algorithms.AES.block_size).unpadder()
    try:
        unpadded_data = unpadder.update(decrypted_data) + unpadder.finalize()
        return unpadded_data.decode('utf-8')  # Assuming plaintext is UTF-8 encoded
    except ValueError as e:
        raise Exception ('AES decryption failed:', e)


# wrapper for trying the different decryption methods
def decrypt(encrypted_data, passphrase):
    logger = getFwoLogger()
    try:
        decrypted = decrypt_aes_ciphertext(encrypted_data, passphrase)
        return decrypted
    except:
        logger.warning("Unspecified error while decrypting with MS: " + str(traceback.format_exc()))
        return encrypted_data
