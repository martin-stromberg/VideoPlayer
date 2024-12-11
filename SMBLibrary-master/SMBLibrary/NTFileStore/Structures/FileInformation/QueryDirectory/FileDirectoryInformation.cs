/* Copyright (C) 2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace SMBLibrary
{
    /// <summary>
    /// [MS-FSCC] 2.4.10 - FileDirectoryInformation
    /// </summary>
    public class FileDirectoryInformation : QueryDirectoryFileInformation
    {
        public const int FixedLength = 64;

        public DateTime CreationTime;
        public DateTime LastAccessTime;
        public DateTime LastWriteTime;
        public DateTime ChangeTime;
        public long EndOfFile;
        public long AllocationSize;
        public FileAttributes FileAttributes;
        private uint FileNameLength;
        public string FileName = String.Empty;

        public FileDirectoryInformation()
        {
        }

        public FileDirectoryInformation(byte[] buffer, int offset) : base(buffer, offset)
        {
            try { CreationTime = DateTime.FromFileTimeUtc(LittleEndianConverter.ToInt64(buffer, offset + 8)); } catch { CreationTime = DateTime.MinValue; }
            try { LastAccessTime = DateTime.FromFileTimeUtc(LittleEndianConverter.ToInt64(buffer, offset + 16)); } catch { LastAccessTime = DateTime.MinValue; }
            try { LastWriteTime = DateTime.FromFileTimeUtc(LittleEndianConverter.ToInt64(buffer, offset + 24)); } catch { LastWriteTime = DateTime.MinValue; }
            try { ChangeTime = DateTime.FromFileTimeUtc(LittleEndianConverter.ToInt64(buffer, offset + 32)); } catch { ChangeTime = DateTime.MinValue; }
            try { EndOfFile = LittleEndianConverter.ToInt64(buffer, offset + 40); } catch { EndOfFile = 0; }
            try { AllocationSize = LittleEndianConverter.ToInt64(buffer, offset + 48); } catch { AllocationSize = 0; }
            try { FileAttributes = (FileAttributes)LittleEndianConverter.ToUInt32(buffer, offset + 56); } catch { FileAttributes = 0; }
            try { FileNameLength = LittleEndianConverter.ToUInt32(buffer, offset + 60); } catch { FileNameLength = 0; }
            try { FileName = ByteReader.ReadUTF16String(buffer, offset + 64, (int)FileNameLength / 2); } catch { FileName = string.Empty; }
        }

        public override void WriteBytes(byte[] buffer, int offset)
        {
            base.WriteBytes(buffer, offset);
            FileNameLength = (uint)(FileName.Length * 2);
            LittleEndianWriter.WriteInt64(buffer, offset + 8, CreationTime.ToFileTimeUtc());
            LittleEndianWriter.WriteInt64(buffer, offset + 16, LastAccessTime.ToFileTimeUtc());
            LittleEndianWriter.WriteInt64(buffer, offset + 24, LastWriteTime.ToFileTimeUtc());
            LittleEndianWriter.WriteInt64(buffer, offset + 32, ChangeTime.ToFileTimeUtc());
            LittleEndianWriter.WriteInt64(buffer, offset + 40, EndOfFile);
            LittleEndianWriter.WriteInt64(buffer, offset + 48, AllocationSize);
            LittleEndianWriter.WriteUInt32(buffer, offset + 56, (uint)FileAttributes);
            LittleEndianWriter.WriteUInt32(buffer, offset + 60, FileNameLength);
            ByteWriter.WriteUTF16String(buffer, offset + 64, FileName);
        }

        public override FileInformationClass FileInformationClass
        {
            get
            {
                return FileInformationClass.FileDirectoryInformation;
            }
        }

        public override int Length
        {
            get
            {
                return FixedLength + FileName.Length * 2;
            }
        }
    }
}
