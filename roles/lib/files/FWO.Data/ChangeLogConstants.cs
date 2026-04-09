namespace FWO.Data
{
    public enum ChangeLogFamily
    {
        Manual,
        Import
    }

    public enum ChangeLogObject
    {
        Matrix,
        Management,
        Gateway,
        Error
    }

    public enum ChangeLogOperation
    {
        Create,
        Update,
        Delete,
        SetRemoved,
        Disable,
        Activate,
        Error
    }

    public enum PromptLogEvent
    {
        Created,
        Dismissed,
        Completed
    }

    public enum ChangeLogOrigin
    {
        UiSettings,
        Autodiscovery,
        Import
    }
}
