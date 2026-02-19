using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace IGAE_GUI
{
    /// <summary>
    /// Wrapper thread-safe pour IGA_File
    /// </summary>
    public class ThreadSafeIGAFile
    {
        private readonly IGA_File _innerFile;
        private readonly ReaderWriterLockSlim _fileLock = new();

        public ThreadSafeIGAFile(IGA_File file)
        {
            _innerFile = file;
        }

        public IGA_File InnerFile
        {
            get
            {
                _fileLock.EnterReadLock();
                try
                {
                    return _innerFile;
                }
                finally
                {
                    _fileLock.ExitReadLock();
                }
            }
        }

        public void ExtractFileSafe(uint index, string outputPath, out int result)
        {
            _fileLock.EnterReadLock();
            try
            {
                _innerFile.ExtractFile(index, outputPath, out result);
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public void ExtractFileSafe(uint index, Stream output, out int result)
        {
            _fileLock.EnterReadLock();
            try
            {
                _innerFile.ExtractFile(index, output, out result, leaveOpen: true);
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public string GetFileName(uint index)
        {
            _fileLock.EnterReadLock();
            try
            {
                return _innerFile.names[index];
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public uint GetFileCount()
        {
            _fileLock.EnterReadLock();
            try
            {
                return _innerFile.numberOfFiles;
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public void Close()
        {
            _fileLock.EnterWriteLock();
            try
            {
                _innerFile.Close();
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Wrapper thread-safe pour IGZ_File
    /// </summary>
    public class ThreadSafeIGZFile
    {
        private readonly IGZ.IGZ_File _innerFile;
        private readonly ReaderWriterLockSlim _fileLock = new();

        public ThreadSafeIGZFile(IGZ.IGZ_File file)
        {
            _innerFile = file;
        }

        public IGZ.IGZ_File InnerFile
        {
            get
            {
                _fileLock.EnterReadLock();
                try
                {
                    return _innerFile;
                }
                finally
                {
                    _fileLock.ExitReadLock();
                }
            }
        }

        public void ExtractImageSafe(int imageIndex, Stream output)
        {
            _fileLock.EnterReadLock();
            try
            {
                // À adapter selon votre API IGZ
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }

        public void Close()
        {
            _fileLock.EnterWriteLock();
            try
            {
                _innerFile.ebr?.Close();
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }
    }
}
