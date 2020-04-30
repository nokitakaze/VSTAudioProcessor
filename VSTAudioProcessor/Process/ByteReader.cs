using System;
using System.Linq;
using System.Text;

namespace VSTAudioProcessor.Process
{
    public class ByteReader
    {
        protected readonly byte[] Stream;
        protected uint Offset;

        public ByteReader(byte[] stream)
        {
            Stream = stream;
        }

        public void Seek(uint newOffset)
        {
            Offset = newOffset;
            if (Offset >= Stream.Length)
            {
                throw new Exception("Seek is too far");
            }
        }

        public void SeekPlus(uint newOffset)
        {
            Offset += newOffset;
            if (Offset >= Stream.Length)
            {
                throw new Exception("Seek is too far");
            }
        }

        public long GetLeftSize()
        {
            return this.Stream.Length - Offset;
        }

        public uint ReadUint()
        {
            var b = new byte[4];
            Array.Copy(Stream, Offset, b, 0, 4);
            Offset += 4;
            return BitConverter.ToUInt32(b.Reverse().ToArray(), 0);
        }

        public float ReadFloat()
        {
            var b = new byte[4];
            Array.Copy(Stream, Offset, b, 0, 4);
            Offset += 4;
            return BitConverter.ToSingle(b.Reverse().ToArray(), 0);
        }

        public string Read4CharString()
        {
            var s = Encoding.ASCII.GetString(Stream, (int) Offset, 4);
            Offset += 4;

            return s;
        }

        public byte[] ReadBytes(uint size)
        {
            var bytes = new byte[size];
            Array.Copy(Stream, Offset, bytes, 0, size);
            Offset += size;

            return bytes;
        }
    }
}