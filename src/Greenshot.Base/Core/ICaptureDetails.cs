/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Ported to Linux/Ubuntu by the Greenshot contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 */

using Greenshot.Base.Core.Enums;

namespace Greenshot.Base.Core;

public interface ICaptureDetails
{
    string Title { get; set; }
    string Filename { get; set; }
    DateTime DateTime { get; set; }
    CaptureMode CaptureMode { get; set; }
    string WindowHandle { get; set; }

    Dictionary<string, string> MetaData { get; }
    void AddMetaData(string key, string value);
}
