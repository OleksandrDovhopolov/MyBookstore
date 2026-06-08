using System;
using Cysharp.Threading.Tasks;

namespace Game.Http {
    public interface IRequest {
        void AddHeader(string name, string value);
        void SetHeader(string name, string value);

        void AddField(string name, string value);
        void AddBinaryData(string name, byte[] value);

        void SetRawData(byte[] data);

        void Send();
        UniTask SendAsync();
        void Abort();

        RequestStates State { get; }
        Exception Exception { get; }

        TimeSpan RequestTimeout { get; set; }
    }
}
