using System;
using System.Collections.Generic;
using Hullbreach.Platform;

namespace Hullbreach.Platform.Tests
{
    // Records requests and answers them either at once or when the test says so.
    sealed class FakePlatformHttp : IPlatformHttp
    {
        readonly Queue<PlatformHttpResponse> _scripted = new Queue<PlatformHttpResponse>();
        readonly List<Action<PlatformHttpResponse>> _held = new List<Action<PlatformHttpResponse>>();

        public readonly List<PlatformHttpRequest> Requests = new List<PlatformHttpRequest>();

        // When true, Send parks the callback until Complete is called.
        public bool Hold;

        public void Enqueue(PlatformHttpResponse response) => _scripted.Enqueue(response);

        public void Send(PlatformHttpRequest request, Action<PlatformHttpResponse> onComplete)
        {
            Requests.Add(request);
            if (Hold)
            {
                _held.Add(onComplete);
                return;
            }
            onComplete(_scripted.Count > 0 ? _scripted.Dequeue() : PlatformHttpResponse.Transport("no scripted response"));
        }

        public int HeldCount => _held.Count;

        public void Complete(int index, PlatformHttpResponse response) => _held[index](response);
    }

    // Captures log lines so tests can prove secrets never reach them.
    sealed class RecordingPlatformLog : IPlatformLog
    {
        public readonly List<string> Lines = new List<string>();

        public void Info(string message) => Lines.Add("INFO " + message);

        public void Warning(string message) => Lines.Add("WARN " + message);

        public string All => string.Join("\n", Lines);
    }
}
