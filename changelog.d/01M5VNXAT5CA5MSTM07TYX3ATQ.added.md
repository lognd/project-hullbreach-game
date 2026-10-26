The host now decodes incoming `ReadyUp` messages into `ServerSimulation.SetReady` via `HostInbox.Dispatch`, so the ready-up handshake works over a real transport and not only in tests.
