# Security Policy

## Scope

This repository is the game side of Project Hullbreach: the Unity client
and the dedicated game server built from the same project. Accounts,
sessions, ELO, match history, and the store live in the separate platform
repository and have their own policy.

## Supported versions

Only the `main` branch is supported. There are no release branches.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Email
logan@logandapp.com with a description, steps to reproduce, and the
commit hash you tested against. You will get an acknowledgement within
three days and a fix or a mitigation plan within two weeks for anything
that lets a client cheat, crash the server, or impersonate another player.

If you are course staff and find something during grading, the same
address works, or flag it in the grading feedback.

## What we consider in scope

Anything a modified client can do that the server should have refused:
moving or firing outside the rules, forging match results, reading other
players' state it should not see. Also anything that crashes or hangs the
dedicated server from the network, and secrets committed to the
repository.

## What we do ourselves

The server is authoritative: Netcode for Entities simulates on the server
and clients only send input. Match results are reported to the platform
API by the server, never by a client. No API keys or service credentials
are committed; Unity Gaming Services are linked per developer through the
editor.
