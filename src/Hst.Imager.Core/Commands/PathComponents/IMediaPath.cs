namespace Hst.Imager.Core.Commands.PathComponents;

public interface IMediaPath
{
    char PathSeparator { get; }
    string[] Split(string path);
    string Join(string[] pathComponents);
}