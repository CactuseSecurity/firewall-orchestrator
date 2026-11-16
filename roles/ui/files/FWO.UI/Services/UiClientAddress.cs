using FWO.Basics;
using System.Net;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Address of the browser of one UI session. Registered as scoped service, so one instance exists per circuit.
    /// </summary>
    /// <remarks>
    /// A circuit has no HTTP request of its own, so the address is read while the page is requested (_Host.cshtml)
    /// and handed to the circuit as parameter of the root component. The framework protects root component
    /// parameters against changes by the browser.
    /// </remarks>
    public sealed class UiClientAddress
    {
        /// <summary>
        /// Canonical text of the address, empty as long as it is not known.
        /// </summary>
        public string Value { get; private set; } = "";

        /// <summary>
        /// The address for a log message, or a placeholder as long as it is not known.
        /// </summary>
        public string Description => Value == "" ? ClientAddressFormat.kUnknown : Value;

        /// <summary>
        /// The address as login option for the middleware; null as long as it is not known.
        /// </summary>
        public string? LoginOption => Value == "" ? null : Value;

        /// <summary>
        /// Stores the address. Anything but a single canonical address is taken as unknown.
        /// </summary>
        /// <param name="address">Address text as written by <see cref="ClientAddressFormat.ToText"/>.</param>
        public void Set(string? address)
        {
            Value = ClientAddressFormat.TryParse(address, out IPAddress? parsed) ? ClientAddressFormat.ToText(parsed) : "";
        }
    }
}
