﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RatchetEdit.DataFunctions;

namespace RatchetEdit
{
    public class Type7C
    {
        public const int ELEMENTSIZE = 0x20;

        public float off_00;
        public float off_04;
        public float off_08;
        public float off_0C;

        public int off_10;
        public int off_14;
        public int off_18;
        public int off_1C;

        public Type7C(byte[] block, int num)
        {
            int offset = num * ELEMENTSIZE;

            off_00 = ReadFloat(block, offset + 0x00);
            off_04 = ReadFloat(block, offset + 0x04);
            off_08 = ReadFloat(block, offset + 0x08);
            off_0C = ReadFloat(block, offset + 0x0C);

            off_10 = ReadInt(block, offset + 0x10);
            off_14 = ReadInt(block, offset + 0x14);
            off_18 = ReadInt(block, offset + 0x18);
            off_1C = ReadInt(block, offset + 0x1C);
        }

        public byte[] Serialize()
        {
            byte[] bytes = new byte[ELEMENTSIZE];

            WriteFloat(ref bytes, 0x00, off_00);
            WriteFloat(ref bytes, 0x04, off_04);
            WriteFloat(ref bytes, 0x08, off_08);
            WriteFloat(ref bytes, 0x0C, off_0C);

            WriteInt(ref bytes, 0x10, off_10);
            WriteInt(ref bytes, 0x14, off_14);
            WriteInt(ref bytes, 0x18, off_18);
            WriteInt(ref bytes, 0x1C, off_1C);

            return bytes;
        }
    }
}
