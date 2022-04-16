using System;
using System.Buffers.Binary;
using System.IO;

namespace AssetStudio
{
    public class EndianBinaryWriter : BinaryWriter
    {
        private readonly byte[] buffer;

        public EndianType Endian;

        public EndianBinaryWriter(Stream stream, EndianType endian = EndianType.BigEndian) : base(stream)
        {
            Endian = endian;
            buffer = new byte[8];
        }

        public long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public override void Write(short value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(buffer, value);
                Write(buffer, 0, 2);
                return;
            }
            base.Write(value);
        }

        public override void Write(int value)
        {
            if (Endian == EndianType.BigEndian)
            {
                 BinaryPrimitives.WriteInt32BigEndian(buffer, value);

                Write(buffer, 0, 4);

                return;
            }
            base.Write(value);
        }

        public override void Write(long value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(buffer, value);

                Write(buffer, 0, 8);
                return;
            }
             base.Write(value);
        }

        public override void Write(ushort value)
        {
            if (Endian == EndianType.BigEndian)
            {
                 BinaryPrimitives.WriteUInt16BigEndian(buffer, value);

                Write(buffer, 0, 2);
                return;
            }
            base.Write(value);
        }

        public override void Write(uint value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

                Write(buffer, 0, 4);
                return;
            }
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteUInt64BigEndian(buffer, value);

                Write(buffer, 0, 8); ;
                return;
            }
            Write(value);
        }

        public override void Write(float value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteSingleBigEndian(buffer, value);
                Write(buffer, 0, 4);
                return;
            }
            base.Write(value);
        }

        public override void Write(double value)
        {
            if (Endian == EndianType.BigEndian)
            {
                BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
                Write(buffer, 0, 8);
                return;
            }
            base.Write(value);
        }
    }
}
