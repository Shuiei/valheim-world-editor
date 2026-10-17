using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// A seed's layout under way, kept to go on later without starting again (the dungeons after the
    /// start, bosses and traders): the game's kinds laid out in its order so far, and its biome map.
    /// One caller at a time.
    /// </summary>
    public sealed class Layout
    {
        /// <summary>
        /// The seed's generator.
        /// </summary>
        private readonly WorldGenerator _gen;
        /// <summary>
        /// Whether the biome map is built on every core but one.
        /// </summary>
        private readonly bool _parallel;
        /// <summary>
        /// The rules in use when the layout began, kept even if Use swaps them.
        /// </summary>
        private readonly State _state = SeedLocations._state;
        /// <summary>
        /// Lets one caller at a time go on with the layout.
        /// </summary>
        private readonly Lock _sync = new();
        /// <summary>
        /// The biome map, built when a kind first needs it.
        /// </summary>
        private BiomeMap? _map;
        /// <summary>
        /// The instances so far; a new one after a stopped or failed layout.
        /// </summary>
        private Placer _placer;
        /// <summary>
        /// How many kinds of the game's order are laid out.
        /// </summary>
        private int _done;
        /// <summary>
        /// The running call's token, for the biome map built during it.
        /// </summary>
        private CancellationToken _cancel;

        /// <summary>
        /// A layout of a seed, nothing laid out yet.
        /// </summary>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="parallel">The biome map on every core but one.</param>
        public Layout(WorldGenerator gen, bool parallel = false)
        {
            _gen = gen;
            _parallel = parallel;
            _placer = NewPlacer();
        }

        /// <summary>
        /// A placer with no instances, sharing this layout's biome map.
        /// </summary>
        private Placer NewPlacer() => new(_gen, () => _map ??= new BiomeMap(_gen, _parallel, _cancel));

        /// <summary>
        /// The instances so far, laid out at least as far as <paramref name="which"/> says, the uncertain kinds
        /// left out.
        /// </summary>
        public List<Placed> Place(Which which, CancellationToken cancel = default)
        {
            int count = which switch
            {
                Which.Start => 1,
                Which.Prioritized => _state.Ordered.Count(r => r.Prioritized != 0),
                _ => _state.Ordered.Count,
            };
            return [.. Until(count, cancel).Where(p => !Uncertain(_state, p.Rule))];
        }

        /// <summary>
        /// The instances of these kinds (prefab names), laid out as far as the last of them; kinds an alt biome
        /// adds left out.
        /// </summary>
        public List<Placed> PlaceKinds(IReadOnlyCollection<string> prefabs, CancellationToken cancel = default)
        {
            int count = _state.Ordered.FindLastIndex(r => prefabs.Contains(r.Prefab)) + 1;
            return [.. Until(count, cancel).Where(p => prefabs.Contains(p.Rule.Prefab) && p.Rule.AltBiome == null)];
        }

        /// <summary>
        /// The first count kinds of the game's order laid out (those already done kept): every instance
        /// so far. Stopped or failed half way, it starts again next time (a kind half laid out is no use).
        /// The thread's Random state is left as it was.
        /// </summary>
        private List<Placed> Until(int count, CancellationToken cancel)
        {
            lock (_sync)
            {
                _cancel = cancel;
                Rnd.RandomState saved = Rnd.State;
                try
                {
                    for (; _done < Math.Min(count, _state.Ordered.Count); _done++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        _placer.Generate(_state.Ordered[_done]);
                    }
                }
                catch
                {
                    // Stopped or failed half way through a kind: nothing of it is kept.
                    _placer = NewPlacer();
                    _done = 0;
                    throw;
                }
                finally
                {
                    Rnd.State = saved;
                }
                return [.. _placer.Instances.Values];
            }
        }
    }
}
