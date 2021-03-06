﻿#region License Header

// /***************************************************************************
//  *   Copyright (c) 2011 OpenUO Software Team.
//  *   All Right Reserved.
//  *
//  *   GumpImageSourceAdapter.cs
//  *
//  *   This program is free software; you can redistribute it and/or modify
//  *   it under the terms of the GNU General Public License as published by
//  *   the Free Software Foundation; either version 3 of the License, or
//  *   (at your option) any later version.
//  ***************************************************************************/

#endregion

#region Usings

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenUO.Ultima.Adapters;

#endregion

namespace OpenUO.Ultima.PresentationFramework.Adapters
{
    internal class GumpImageSourceAdapter : StorageAdapterBase, IGumpStorageAdapter<ImageSource>
    {
        private FileIndexBase _fileIndex;

        public override int Length
        {
            get
            {
                if (!IsInitialized)
                {
                    Initialize();
                }

                return _fileIndex.Length;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            InstallLocation install = Install;

            _fileIndex =
                install.IsUOPFormat
                    ? install.CreateFileIndex("gumpartLegacyMUL.uop", 0xFFFF, true, ".tga")
                    : install.CreateFileIndex("gumpidx.mul", "gumpart.mul");
        }

        public unsafe ImageSource GetGump(int index)
        {
            int length, extra;
            Stream stream = _fileIndex.Seek(index, out length, out extra);

            if (stream == null)
            {
                return null;
            }

            BinaryReader bin = new BinaryReader(stream);

            int width = (extra >> 16) & 0xFFFF;
            int height = extra & 0xFFFF;

            WriteableBitmap bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr555, null);
            bmp.Lock();

            int[] lookups = new int[height];
            int start = (int)bin.BaseStream.Position;

            for (int i = 0; i < height; ++i)
            {
                lookups[i] = start + (bin.ReadInt32() * 4);
            }

            ushort* line = (ushort*)bmp.BackBuffer;
            ushort delta = (ushort)(bmp.BackBufferStride >> 1);

            for (int y = 0; y < height; ++y, line += delta)
            {
                bin.BaseStream.Seek(lookups[y], SeekOrigin.Begin);

                ushort* cur = line;
                ushort* end = line + bmp.PixelWidth;

                while (cur < end)
                {
                    ushort color = bin.ReadUInt16();
                    ushort* next = cur + bin.ReadUInt16();

                    if (color == 0)
                    {
                        cur = next;
                    }
                    else
                    {
                        color ^= 0x8000;

                        while (cur < next)
                        {
                            *cur++ = color;
                        }
                    }
                }
            }

            bmp.AddDirtyRect(new Int32Rect(0, 0, width, height));
            bmp.Unlock();

            return bmp;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_fileIndex != null)
            {
                _fileIndex.Close();
                _fileIndex = null;
            }
        }
    }
}