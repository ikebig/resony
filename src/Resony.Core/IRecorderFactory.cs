﻿namespace Resony
{
    public interface IRecorderFactory
    {
        RecorderBase Create(Device device);
        RecorderBase Create(Device device, Format format);
    }
}
