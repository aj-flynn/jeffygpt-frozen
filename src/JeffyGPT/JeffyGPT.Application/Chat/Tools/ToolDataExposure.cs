namespace JeffyGPT.Application.Chat.Tools;

public enum ToolDataExposure
{
    None,
    RuntimeMetadata,
    LocalFileSystem,
    ExternalService,
    UserPrivateData
}
