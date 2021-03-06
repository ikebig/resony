﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Resony
{
    public abstract class RecorderBase : IDisposable
    {
        public RecorderBase(Device device)
            : this(device, Format.Default())
        {
        }

        public RecorderBase(Device device, Format format)
        {
            Device = device;
            Format = format;
        }

        private int SampleAggregationTimeoutMilliseconds { get; } = Constants.Audio.Recording.SampleAggregationTimeoutMilliseconds;

        public abstract event DataAvailableHandler DataAvailable;

        public Device Device { get; }
        public Format Format { get; }

        public abstract RecorderState Status { get; }
        public abstract bool Start();
        public abstract bool Stop();

        #region Recordings

        /// <summary>
        /// Records from device and returns the recorded byte samples
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>byte array</returns>
        public byte[] Record(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            using (var ms = new MemoryStream())
            {
                Record(ms, duration, cancellationToken);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Records byte samples to the provided stream.
        /// </summary>
        /// <param name="outStream"></param>
        /// <param name="duration"></param>
        /// <param name="cancellationToken"></param>
        public void Record(Stream outStream, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            if (Status != RecorderState.Playing)
            {
                throw new AuralsysAudioException($"Invalid {nameof(RecorderState)} from device '{Device}'.");
            }

            long totalBytesRead = 0;
            long totalBytesToRead = (long)(duration.TotalSeconds * Format.Channels * Format.SampleRate * Format.BitDepth.GetBlockAlign());
            bool faulted = false;

            void aggregation(DataAvailableArgs args)
            {
                try
                {
                    if (args.Length <= 0)
                    {
                        throw new AuralsysAudioException("Number of bytes read is negative or zero.");
                    }

                    if (totalBytesRead < totalBytesToRead)
                    {
                        byte[] chunk = new byte[args.Length];
                        if (totalBytesRead + args.Length > totalBytesToRead)
                        {
                            chunk = new byte[totalBytesToRead - totalBytesRead];
                        }

                        Array.Copy(args.Buffer, 0, chunk, 0, chunk.Length);
                        totalBytesRead += chunk.Length;
                        outStream.Write(chunk, 0, chunk.Length);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"Recording error from device '{Device}'. {ex.Message}", ex);
                    faulted = true;
                }
            }

            DataAvailable += aggregation;
            while (Status == RecorderState.Playing && totalBytesRead < totalBytesToRead && !faulted && !cancellationToken.IsCancellationRequested)
            {
                Task.Delay(SampleAggregationTimeoutMilliseconds).Wait();
            }
            DataAvailable -= aggregation;
        }

        /// <summary>
        /// Records from device and returns byte array
        /// </summary>
        /// <param name="duration"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<byte[]> RecordAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var samples = Record(duration, cancellationToken);
                return samples;
            }, cancellationToken);
        }

        /// <summary>
        /// Records byte samples to the provided stream
        /// </summary>
        /// <param name="outStream"></param>
        /// <param name="duration"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task RecordAsync(Stream outStream, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Record(outStream, duration, cancellationToken), cancellationToken);
        }

        #endregion

        #region Wave file recording

        public abstract void RecordWaveFile(Stream stream, TimeSpan duration, CancellationToken cancellationToken = default);

        public void RecordWaveFile(string path, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(path);
            string tempPath = IOHelper.GetTemporaryFilePath(fileInfo);

            using (var stream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                RecordWaveFile(stream, duration, cancellationToken);
            }

            if (File.Exists(tempPath))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
        }

        public async Task RecordWaveFileAsync(string path, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => RecordWaveFile(path, duration, cancellationToken), cancellationToken);
        }

        public async Task RecordWaveFileAsync(Stream stream, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => RecordWaveFile(stream, duration, cancellationToken), cancellationToken);
        }

        #endregion

        public abstract void Dispose();
    }
}
