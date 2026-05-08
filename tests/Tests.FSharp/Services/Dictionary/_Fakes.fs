module Stem.ButtonPanel.Tester.Tests.Services.Dictionary.Fakes

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.FSharp.Collections
open Stem.ButtonPanel.Tester.Core.Dictionary
open Stem.ButtonPanel.Tester.Services.Dictionary

let sampleDictionary : ButtonPanelDictionary = {
    SchemaVersion = 1
    GeneratedAt = DateTimeOffset(2026, 5, 8, 10, 0, 0, TimeSpan.Zero)
    PanelTypes = [
        {
            Id = "2"
            DisplayName = "Pulsantiere"
            Variables = [
                {
                    Name = "Foto Tasti"
                    Type = "UInt8"
                    Address = (128 <<< 8) ||| 0
                    Scaling = 1.0
                    Unit = ""
                }
            ]
        }
    ]
}

/// Manual fake for `IDictionaryProvider`: returns a queue of pre-canned
/// results in order (first call returns first result, etc.). Throws
/// InvalidOperationException if called more times than results are queued.
type QueueingProvider(results: DictionaryFetchResult list) =
    let mutable queue = results
    let mutable callCount = 0
    member _.CallCount = callCount
    interface IDictionaryProvider with
        member _.FetchAsync(_ct: CancellationToken) =
            callCount <- callCount + 1
            match queue with
            | [] -> Task.FromResult(Failed(NetworkUnreachable, Some "queue exhausted"))
            | head :: rest ->
                queue <- rest
                Task.FromResult head

/// Manual fake for `IDictionaryCacheWriter`: records every WriteAsync call.
type RecordingCacheWriter() =
    let writes = ResizeArray<ButtonPanelDictionary * DateTimeOffset>()
    member _.Writes : (ButtonPanelDictionary * DateTimeOffset) seq = writes :> _ seq
    interface IDictionaryCacheWriter with
        member _.WriteAsync(dictionary, fetchedAt, _ct) =
            writes.Add((dictionary, fetchedAt))
            Task.CompletedTask

/// Cache writer that throws — for verifying the live success path is robust
/// to write failures (cache is best-effort).
type ThrowingCacheWriter(ex: exn) =
    interface IDictionaryCacheWriter with
        member _.WriteAsync(_dict, _fetchedAt, _ct) =
            Task.FromException(ex)
