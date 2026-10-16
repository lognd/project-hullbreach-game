"""Error sets for every fallible boundary the tools cross (process, HTTP, files)."""

from __future__ import annotations

from typani import ErrorSet


class ProcError(ErrorSet):
    """Why running a program failed."""

    NotFound = "the program is not installed or not on PATH"
    TimedOut = "the program did not finish in time"
    Failed = "the program exited with an error"
    StartFailed = "the program could not be started"


class HttpError(ErrorSet):
    """Why an HTTP request got no usable answer."""

    Unreachable = "nothing answered at that address"
    TimedOut = "the server did not answer in time"
    BadUrl = "the address is not a valid http(s) URL"
    BadBody = "the server answered with something that is not JSON"


class ProjectError(ErrorSet):
    """Why a project file could not be read."""

    Missing = "the file does not exist"
    Unreadable = "the file could not be read"
    Malformed = "the file does not have the expected content"


class PlatformError(ErrorSet):
    """Why a platform API call did not succeed."""

    Unreachable = "the platform did not answer"
    BadCredentials = "the username or password is wrong"
    RateLimited = "too many sign-in attempts; wait a minute"
    Refused = "the platform refused the request"
    Unexpected = "the platform answered in an unexpected way"


class ToolError(ErrorSet):
    """Why a play/build command could not go ahead."""

    NoEditor = "Unity 6000.0.43f1 was not found"
    NoBuild = "there is no build yet"
    EditorBusy = "Unity already has this project open"
    BuildFailed = "the Unity build failed"
    Cancelled = "cancelled by the user"
    Unsupported = "this command is not supported on this computer"
