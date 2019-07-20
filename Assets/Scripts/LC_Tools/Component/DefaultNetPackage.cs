using System;
using HiSocket;


namespace LC_Tools
{
    public class DefaultNetPackage : IPackage
    {
        private const int _intLen = sizeof(int);

        /// <summary>
        /// Pack your message here(this is only an example)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="unpackedHandler"></param>
        public void Unpack(IByteArray source, Action<byte[]> unpackedHandler)
        {
//            Debug.LogWarning($"-------------- !!!! Receive Len:[{source.Length}] --------------");

            while (source.Length >= _intLen)
            {
                var len = source.Read(_intLen);
                var bodyLength = BitConverter.ToInt32(len, 0); // get body's length
                if (bodyLength <= 0)
                    continue;
                var dataLen = bodyLength - _intLen;
//                Debug.LogError($"!!!! wait process Sum:[{bodyLength}] Data:[{dataLen}] Count:[{count}] ");
                if (source.Length >= dataLen)
                {
                    var unpacked = source.Read(dataLen); // get body
                    unpackedHandler(unpacked);
                }
                else
                {
                    source.Insert(0, len); // rewrite in, used for next time
//                    Debug.LogError($"@@@@@@@@@@@@@@@ Length Error notify:{bodyLength} actual:{source.Length} @@@@@@@@@@@@@@@");
                    break;
                }
            }
        }

        /// <summary>
        /// Unpack your message here(this is only an example)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="packedHandler"></param>
        public void Pack(IByteArray source, Action<byte[]> packedHandler)
        {
            packedHandler(source.Read(source.Length));
        }
    }
}