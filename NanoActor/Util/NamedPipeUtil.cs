using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor.Util
{
    public static class NamedPipeUtil
    {
       

        public  static Task WriteMessageToPipe(PipeStream pipe, string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            return WriteMessageToPipe(pipe, messageBytes);
        }
        public static async Task WriteMessageToPipe(PipeStream pipe, byte[] data)
        {
            var header = BitConverter.GetBytes(data.Length);
            var wrappedMessage = header.Concat(data).ToArray();

            //pipe.Write(wrappedMessage, 0, wrappedMessage.Length);

            //lock (pipe)
            //{
            //    pipe.Write(wrappedMessage, 0, wrappedMessage.Length);
            //    pipe.Flush();
            //}

            await pipe.WriteAsync(wrappedMessage, 0, wrappedMessage.Length);
            await pipe.FlushAsync();

        }

        public static async Task<byte[]> ReadBytesFromFipe(PipeStream stream,int bytes)
        {
            var buffer = new byte[bytes];

            var read = 0;

            while (read<bytes && stream.IsConnected)
            {                               
               read += await stream.ReadAsync(buffer, read, bytes-read);
            }

            return buffer;

        }

        public static async Task<byte[]> ReadMessageFromPipe(PipeStream stream)
        {

            var headerBuffer = await ReadBytesFromFipe(stream,4);

            var messageLenght = BitConverter.ToInt32(headerBuffer, 0);

            //read message
            var messageBuffer = await ReadBytesFromFipe(stream, messageLenght);


            return messageBuffer;
        }

    }
}
