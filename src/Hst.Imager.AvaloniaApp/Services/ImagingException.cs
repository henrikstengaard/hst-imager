using System;

namespace Hst.Imager.AvaloniaApp.Services;

/// <summary>
/// Raised when an imaging or media command returns a faulted result.
/// </summary>
public class ImagingException(string message) : Exception(message);
