﻿using ManagedBass;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Auralsys.Audio.ManagedBass
{
    public static class RecordBaseExtensions
    {
        public static void WriteWaveFile(this RecorderBase recorder, Stream stream, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            if (recorder == null || duration == TimeSpan.Zero)
            {
                return;
            }

            if (recorder.Status != RecorderState.Playing)
            {
                throw new BassException($"Invalid {nameof(RecorderState)}.");
            }

            long totalBytesRead = 0;
            long totalBytesToRead = (long)(duration.TotalSeconds * recorder.Format.Channels * recorder.Format.SampleRate * recorder.Format.BitDepth.GetBlockAlign());
            var writer = new WaveFileWriter(stream, recorder.Format.ToWaveFormat());
            bool forceExit = false;

            void aggregation(DataAvailableArgs args)
            {
                try
                {
                    if (args.Length <= 0)
                    {
                        throw new BassException("Number of bytes read is negative or zero.");
                    }

                    if (totalBytesRead < totalBytesToRead)
                    {
                        byte[] chunk = new byte[args.Length];
                        if (totalBytesRead + args.Length > totalBytesToRead)
                        {
                            chunk = new byte[totalBytesToRead - totalBytesRead];
                        }

                        Array.Copy(args.Buffer, 0, chunk, 0, chunk.Length);
                        writer.Write(chunk, chunk.Length);
                        totalBytesRead += chunk.Length;
                    }
                }
                catch (Exception ex)
                {
                    forceExit = true;
                    Trace.TraceError(ex.Message);
                }
            }           

            recorder.DataAvailable += aggregation;
            while (recorder.Status == RecorderState.Playing && totalBytesRead < totalBytesToRead && !forceExit && !cancellationToken.IsCancellationRequested)
            {
                Task.Delay(Constants.Audio.Recording.SampleAggregationTimeoutMilliseconds, cancellationToken).Wait();
            }
            recorder.DataAvailable -= aggregation;
            writer.Dispose();
        }

        public static void WriteWaveFile(this RecorderBase recorder, string path, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            var fileInfo = new FileInfo(path);
            string tempPath = CommonHelper.GetTemporaryFilePath(fileInfo);

            using (var stream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                recorder.WriteWaveFile(stream, duration, cancellationToken);
            }

            if (File.Exists(tempPath))
            {
                File.Move(tempPath, path, true);
            }
        }

        public async static Task WriteWaveFileAsync(this RecorderBase recorder, string path, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => recorder.WriteWaveFile(path, duration, cancellationToken), cancellationToken);
        }

        public async static Task WriteWaveFileAsync(this RecorderBase recorder, Stream stream, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => recorder.WriteWaveFile(stream, duration, cancellationToken), cancellationToken);
        }

    }
}