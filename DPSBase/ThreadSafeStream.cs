﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DPSBase
{
    /// <summary>
    /// A wrapper around a stream to ensure it can be accessed in a thread safe way. The .net implementation of Stream.Synchronized is suitable on its own.
    /// </summary>
    public class ThreadSafeStream : IDisposable
    {
        private Stream stream;
        private object streamLocker = new object();

        /// <summary>
        /// If true the internal stream will be disposed once the data has been written to the network
        /// </summary>
        public bool DisposeStreamAfterSend { get; private set; }

        /// <summary>
        /// Create a thread safe stream. Once any actions are complete the stream must be correctly disposed by the user.
        /// </summary>
        /// <param name="stream">The stream to make thread safe</param>
        public ThreadSafeStream(Stream stream)
        {
            this.DisposeStreamAfterSend = false;

            if (stream.Length > int.MaxValue)
                throw new NotImplementedException("Streams larger than 2GB not yet supported.");

            this.stream = Stream.Synchronized(stream);
            //this.stream = stream;
        }

        /// <summary>
        /// Create a thread safe stream.
        /// </summary>
        /// <param name="stream">The stream to make thread safe.</param>
        /// <param name="disposeStreamAfterSend">If true the provided stream will be disposed once data has been written to the network. If false the stream must be disposed of correctly by the user</param>
        public ThreadSafeStream(Stream stream, bool disposeStreamAfterSend)
        {
            this.DisposeStreamAfterSend = disposeStreamAfterSend;

            if (stream.Length > int.MaxValue)
                throw new NotImplementedException("Streams larger than 2GB not yet supported.");

            this.stream = Stream.Synchronized(stream);
            //this.stream = stream;
        }

        /// <summary>
        /// The total length of the internal stream
        /// </summary>
        public long Length
        {
            get { lock (streamLocker) return stream.Length; }
        }

        /// <summary>
        /// The current position of the internal stream
        /// </summary>
        public long Position
        {
            get { lock (streamLocker) return stream.Position; }
        }

        /// <summary>
        /// Returns data from Stream.ToArray()
        /// </summary>
        /// <param name="numberZeroBytesPrefex">If non zero will append N 0 value bytes to the start of the returned array</param>
        /// <returns></returns>
        public byte[] ToArray(int numberZeroBytesPrefex = 0)
        {
            lock (streamLocker)
            {
                stream.Seek(0, SeekOrigin.Begin);
                byte[] returnData = new byte[stream.Length + numberZeroBytesPrefex];
                stream.Read(returnData, numberZeroBytesPrefex, returnData.Length - numberZeroBytesPrefex);
                return returnData;
            }
        }

        /// <summary>
        /// Return the MD5 hash of the current <see cref="StreamSendWrapper"/> as a string
        /// </summary>
        /// <returns></returns>
        public string MD5CheckSum()
        {
            lock (streamLocker)
            {
                stream.Seek(0, SeekOrigin.Begin);
                System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "");
            }
        }

        /// <summary>
        /// Writes all provided data to the internal stream starting at the provided position with the stream
        /// </summary>
        /// <param name="data"></param>
        /// <param name="startPosition"></param>
        public void Write(byte[] data, int startPosition)
        {
            lock (streamLocker)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Copies data specified by start and length properties from internal stream to the provided stream.
        /// </summary>
        /// <param name="destinationStream">The destination stream to write to</param>
        /// <param name="startPosition"></param>
        /// <param name="length"></param>
        public void CopyTo(Stream destinationStream, int startPosition, int length)
        {
            lock (streamLocker)
            {
                //Initialise the buffer at either the total length or 8KB, which ever is smallest
                byte[] buffer = new byte[length > 8192 ? 8192 : length];

                //Make sure we start in the write place
                stream.Seek(startPosition, SeekOrigin.Begin);
                int totalBytesCopied = 0;
                while (true)
                {
                    int bytesRemaining = length - totalBytesCopied;
                    int read = stream.Read(buffer, 0, (buffer.Length > bytesRemaining ? bytesRemaining : buffer.Length));
                    if (read <= 0) return;
                    destinationStream.Write(buffer, 0, read);
                    totalBytesCopied += read;
                }
            }
        }

        /// <summary>
        /// Call Dispose on the internal stream
        /// </summary>
        public void Dispose()
        {
            lock (streamLocker) stream.Dispose();
        }
    }
}
