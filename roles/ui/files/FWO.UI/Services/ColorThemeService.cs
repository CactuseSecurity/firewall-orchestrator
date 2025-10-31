
using System.Collections.Concurrent;


namespace FWO.Ui.Services
{
    public interface IColorModeServices
    {
        string GetColourMode(string key);
        bool SetColourMode(string key, string mode);
    }
    public class ColorModeServices : IColorModeServices
    {
        private readonly ConcurrentDictionary<string, string> _UserColorModes = new ConcurrentDictionary<string, string>();

        public string GetColourMode(string key)
        {
            string mode = string.Empty;
            if (_UserColorModes.TryGetValue(key, out mode))
            {
                return mode;
            }
            else
            {
                //Did not find
                mode = "light"; //Default mode
            }
            return mode;
        }

        public bool SetColourMode(string key, string mode)
        {
            bool allGood = false;
            if (_UserColorModes.TryAdd(key, mode))
            {
                allGood = true;

            }
            else
            {
                allGood = _UserColorModes.TryUpdate(key, mode, _UserColorModes[key]);
            }
            return allGood;
        }
    }
}