using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LC_Tools
{
    public sealed class BinaryMessage
    {
        private readonly List<byte[]> _sendList = new List<byte[]>();
        private int _readOffset;
        private byte[] _receiveArray;

        public int SendMsgSize { get; private set; }

        public int ProtocolId { get; set; }

        public int ReceiveSize()
        {
            return _receiveArray?.Length ?? 0;
        }

        public static BinaryMessage CreateBinary(byte[] content)
        {
            var binary = new BinaryMessage();

            const int intLen = sizeof(int);
            if (content.Length < intLen)
            {
                Debug.LogError($"== Change Error == len:[{content.Length}]");
                return binary;
            }

            var proctorId = BitConverter.ToInt32(content, 0);
            binary.ProtocolId = proctorId;
            if (content.Length == intLen) return binary;

            var messageLen = content.Length - intLen;
            var message_array = new byte[messageLen];
            Array.Copy(content, intLen, message_array, 0, messageLen);
            binary.SetReceiveBuffer(message_array);
            return binary;
        }
        
        public void SetReceiveBuffer(byte[] data)
        {
            _receiveArray = data;
        }

        public byte[] GetSendBuffer()
        {
            const int int_len = sizeof(int);
            var totalLen = int_len * 2 + SendMsgSize;
            var retArr = new byte[totalLen];

            var startIdx = 0;
            var lenArr = BitConverter.GetBytes(totalLen);
            Array.Copy(lenArr, 0, retArr, startIdx, lenArr.Length);

            var protocolArr = BitConverter.GetBytes(ProtocolId);
            startIdx += int_len;
            Array.Copy(protocolArr, 0, retArr, startIdx, protocolArr.Length);
//            start_idx += type_len;
//            Array.Copy(len_arr, 0, ret_arr, start_idx, len_arr.Length);
            startIdx += int_len;
            foreach (var single in _sendList)
            {
                Array.Copy(single, 0, retArr, startIdx, single.Length);
                startIdx += single.Length;
            }

            return retArr;
        }

        public void Add<T>(T data)
        {
            switch (data)
            {
//            Debug.LogWarning($"========= Add Type: [{data.GetType()}]=========");
                case string _:
                {
                    var dataStr = Convert.ToString(data);
                    var contArr = Encoding.UTF8.GetBytes(dataStr);
                    var contentLen = BitConverter.GetBytes(contArr.Length + 1);

                    const int int_len = sizeof(int);
                    var arrLen = int_len + contArr.Length + 1;
                    var destArr = new byte[arrLen];
                    Array.Copy(contentLen, destArr, contentLen.Length);
                    var startIdx = int_len;
                    Array.Copy(contArr, 0, destArr, startIdx, contArr.Length);
                    AddBufferList(destArr.Length, destArr);
                    break;
                }

                case char[] _:
                {
                    var array = data as Array;
                    var dest = new char[array.Length];
                    for (var i = 0; i < array.Length; i++)
                    {
                        dest[i] = (char) array.GetValue(i);
                    }

                    var bytes = Encoding.Unicode.GetBytes(dest);
                    var arr = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, bytes, 0, bytes.Length);
                    AddBufferList(bytes.Length, arr);
                    break;
                }

                case byte[] _:
                {
                    var array = data as Array;
                    var dest = new byte[array.Length];
                    for (var i = 0; i < array.Length; i++)
                    {
                        dest[i] = (byte) array.GetValue(i);
                    }

                    AddBufferList(dest.Length, dest);
                    break;
                }
            }

            if (!(data is ValueType))
            {
                return;
            }

            var typeLen = 0;
            byte[] destArray = null;
            switch (data)
            {
                //Debug.LogWarning(string.Format("========= Add Type ========= type:[{0}] Len:[{1}]", type_str, len));
                case bool _:
                    typeLen = sizeof(bool);
                    destArray = BitConverter.GetBytes(Convert.ToBoolean(data));
                    break;
                case byte _:
                    typeLen = sizeof(byte);
                    destArray = new[] {Convert.ToByte(data)};
                    break;
                case char _:
                    typeLen = sizeof(char);
                    destArray = BitConverter.GetBytes(Convert.ToChar(data));
                    break;
                case ushort _:
                    typeLen = sizeof(ushort);
                    destArray = BitConverter.GetBytes(Convert.ToUInt16(data));
                    break;
                case short _:
                    typeLen = sizeof(short);
                    destArray = BitConverter.GetBytes(Convert.ToInt16(data));
                    break;
                case uint _:
                    typeLen = sizeof(uint);
                    destArray = BitConverter.GetBytes(Convert.ToUInt32(data));
                    break;
                case int _:
                    typeLen = sizeof(int);
                    destArray = BitConverter.GetBytes(Convert.ToInt32(data));
                    break;
                case ulong _:
                    typeLen = sizeof(ulong);
                    destArray = BitConverter.GetBytes(Convert.ToUInt64(data));
                    break;
                case long _:
                    typeLen = sizeof(long);
                    destArray = BitConverter.GetBytes(Convert.ToInt64(data));
                    break;
                case float _:
                    typeLen = sizeof(float);
                    destArray = BitConverter.GetBytes(Convert.ToSingle(data));
                    break;
                case double _:
                    typeLen = sizeof(double);
                    destArray = BitConverter.GetBytes(Convert.ToDouble(data));
                    break;
            }

            if (destArray != null)
            {
                AddBufferList(typeLen, destArray);
            }
            else
            {
                Debug.LogError("== BinaryMessage Add Length is Zero ==");
            }
        }

        public bool GetBoolean()
        {
            var dest = ReadByte(sizeof(bool));
            return BitConverter.ToBoolean(dest, 0);
        }

        public byte GetByte()
        {
            var dest = ReadByte(sizeof(byte));
            return dest[0];
        }

        public char GetChar()
        {
            var dest = ReadByte(sizeof(char));
            return BitConverter.ToChar(dest, 0);
        }

        public ushort GetUInt16()
        {
            var dest = ReadByte(sizeof(ushort));
            return BitConverter.ToUInt16(dest, 0);
        }

        public short GetInt16()
        {
            var dest = ReadByte(sizeof(short));
            return BitConverter.ToInt16(dest, 0);
        }

        public uint GetUInt32()
        {
            var dest = ReadByte(sizeof(uint));
            return BitConverter.ToUInt32(dest, 0);
        }

        public int GetInt32()
        {
            var dest = ReadByte(sizeof(int));
            return BitConverter.ToInt32(dest, 0);
        }

        public ulong GetUInt64()
        {
            var dest = ReadByte(sizeof(ulong));
            return BitConverter.ToUInt64(dest, 0);
        }

        public long GetInt64()
        {
            var dest = ReadByte(sizeof(long));
            return BitConverter.ToInt64(dest, 0);
        }

        public float GetFloat()
        {
            var dest = ReadByte(sizeof(float));
            return BitConverter.ToSingle(dest, 0);
        }

        public double GetDouble()
        {
            var dest = ReadByte(sizeof(double));
            return BitConverter.ToDouble(dest, 0);
        }

        public char[] GetCharArray()
        {
            var strLen = ReadByte(sizeof(int));
            var readLen = BitConverter.ToInt32(strLen, 0);
            var dest = ReadByte(readLen);
            var charArr = new char[readLen];
            Array.Copy(dest, charArr, readLen);
            return charArr;
        }

        public byte[] GetByteArray()
        {
            var strLen = ReadByte(sizeof(int));
            var readLen = BitConverter.ToInt32(strLen, 0);
            return ReadByte(readLen);
        }

        public string GetString()
        {
            var strLen = ReadByte(sizeof(int));
            if (strLen == null || strLen.Length == 0) return string.Empty;
            var readLen = BitConverter.ToInt32(strLen, 0);
            if (readLen == 0) return string.Empty;
            var dest = ReadByte(readLen);
            return Encoding.UTF8.GetString(dest);
        }

        public void Clear()
        {
            ProtocolId = 0;
            SendMsgSize = 0;
            _sendList.Clear();
            _readOffset = sizeof(int) * 2;
            _receiveArray = new byte[0];
        }

        private void AddBufferList(int typeLen, byte[] arr)
        {
            _sendList.Add(arr);
            SendMsgSize += typeLen;
        }

        private byte[] ReadByte(int len)
        {
            if (len == 0) return new byte[0];
            if (len < 0 || _receiveArray == null)
            {
                Debug.LogError("== BinaryMessage ReadByte receive_array is null ==");
                return new byte[0];
            }

            var lave = _receiveArray.Length - _readOffset;
            if (lave < len)
            {
                Debug.LogError($"== BinaryMessage ReadByte Error ID:[{ProtocolId}] Length:[{lave}]  Get:[{len}]==");
                return new byte[0];
            }

            var dest = new byte[len];
            Array.Copy(_receiveArray, _readOffset, dest, 0, len);
            _readOffset += len;
            return dest;
        }
    }
}