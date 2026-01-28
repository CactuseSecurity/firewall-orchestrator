
namespace FWO.Ui.Services
{

    public interface IUrlSanitizer
    {
        /// Returns a normalized http/https URL or null if invalid/unsafe.
        string? Clean(string input);
    }
}
