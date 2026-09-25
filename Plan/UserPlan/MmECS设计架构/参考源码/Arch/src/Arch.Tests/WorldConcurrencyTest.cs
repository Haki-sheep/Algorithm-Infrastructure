// The static World registry (World.Worlds / World.Create bookkeeping) does not exist
// under PURE_ECS, and neither do the Entity extension methods used below.
#if !PURE_ECS
using Arch.Core;
using Arch.Core.Extensions;
using static NUnit.Framework.Assert;

namespace Arch.Tests;

/// <summary>
///     The <see cref="WorldConcurrencyTest"/> class
///     tests concurrent <see cref="World.Create()"/>/<see cref="World.Destroy"/> against the
///     static <see cref="World.Worlds"/> storage.
///
///     Historically <c>World.Create</c> locked the <see cref="World.Worlds"/> array itself and
///     replaced that array on resize, so after a resize concurrent creators locked different
///     objects and raced: duplicate world ids, lost slot writes (a created world resolving to
///     <c>null</c> through <c>World.Worlds[entity.WorldId]</c>) and cross-wired entity storage.
/// </summary>
[TestFixture]
public sealed class WorldConcurrencyTest
{
    /// <summary>
    ///     Concurrently creates worlds (forcing several <see cref="World.Worlds"/> resizes),
    ///     uses each created world immediately and checks id uniqueness.
    /// </summary>
    [Test]
    public void ConcurrentWorldCreateProducesUniqueUsableWorlds()
    {
        const int threads = 8;
        const int worldsPerThread = 64;

        var created = new World[threads * worldsPerThread];
        using var barrier = new Barrier(threads);

        RunOnThreads(threads, threadIndex =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < worldsPerThread; i++)
            {
                var world = World.Create();
                created[(threadIndex * worldsPerThread) + i] = world;

                // Use the world through the static lookup right away — this is the read path
                // (EntityExtensions/generated accessors) that observed null slots pre-fix.
                var entity = world.Create(new Transform { X = threadIndex, Y = i });
                That(entity.IsAlive(), Is.True);
                That(entity.Get<Transform>().X, Is.EqualTo(threadIndex));
            }
        });

        try
        {
            var ids = new HashSet<int>();
            foreach (var world in created)
            {
                That(world, Is.Not.Null);
                That(ids.Add(world.Id), Is.True, $"Duplicate world id {world.Id} handed out concurrently.");
                That(World.Worlds[world.Id], Is.SameAs(world));
            }
        }
        finally
        {
            foreach (var world in created)
            {
                if (world != null)
                {
                    World.Destroy(world);
                }
            }
        }
    }

    /// <summary>
    ///     Churns concurrent create → use → destroy cycles so id recycling, slot writes and
    ///     resizes interleave across threads.
    /// </summary>
    [Test]
    public void ConcurrentWorldCreateDestroyChurnDoesNotCorruptStaticStorage()
    {
        const int threads = 8;
        const int rounds = 200;

        using var barrier = new Barrier(threads);

        RunOnThreads(threads, threadIndex =>
        {
            barrier.SignalAndWait();
            for (var round = 0; round < rounds; round++)
            {
                var world = World.Create();
                var entity = world.Create(new Transform { X = round, Y = threadIndex });
                That(entity.Get<Transform>().Y, Is.EqualTo(threadIndex));
                That(World.Worlds[world.Id], Is.SameAs(world));
                World.Destroy(world);
            }
        });
    }

    private static void RunOnThreads(int threadCount, Action<int> body)
    {
        var failures = new List<Exception>();
        var workers = new Thread[threadCount];
        for (var t = 0; t < threadCount; t++)
        {
            var threadIndex = t;
            workers[t] = new Thread(() =>
            {
                try
                {
                    body(threadIndex);
                }
                catch (Exception exception)
                {
                    lock (failures)
                    {
                        failures.Add(exception);
                    }
                }
            });
            workers[t].Start();
        }

        foreach (var worker in workers)
        {
            worker.Join();
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }
    }
}
#endif
