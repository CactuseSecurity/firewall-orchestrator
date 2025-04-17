namespace FWO.Ui.Data
{
    /// <summary>
    /// Defines a model for errors that occured on csv file upload
    /// </summary>
    public class CSVFileUploadErrorModel : ErrorBaseModel
    {
        public CSVFileUploadErrorModel() : base()
        {
        }

        /// <summary>
        /// Additional Data/Info
        /// </summary>
        public string? EntryData { get; set; }
    }
}
