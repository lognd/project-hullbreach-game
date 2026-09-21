using System;
using System.Collections.Generic;
using System.Linq;

namespace Hullbreach.Structure
{
    /// <summary>
    /// One converged buckling mode: a load factor and the DOF shape it
    /// belongs to, plus which blocks carry the strain energy of that shape.
    /// </summary>
    public sealed class BucklingMode
    {
        /// <summary>Smallest positive lambda such that K + lambda*K_G is
        /// singular along <see cref="Shape"/>. Less than 1 means the CURRENT
        /// load already exceeds the buckling load for this shape.</summary>
        public float LoadFactor;

        /// <summary>The mode's DOF displacement vector, normalized so its
        /// largest-magnitude component is exactly 1 (a shape, not a
        /// physical displacement: the eigenproblem only fixes it up to
        /// scale).</summary>
        public float[] Shape;

        /// <summary>Per-block fraction (0..1, summing to ~1 over the ship) of
        /// this mode's strain energy phi^T K phi. Answers WHICH blocks fold
        /// in this particular mode.</summary>
        public Dictionary<int, float> BlockParticipation;
    }

    /// <summary>
    /// Linearized buckling: the smallest positive load factors lambda solving
    /// the generalized eigenproblem (K + lambda*K_G) phi = 0, i.e.
    /// K phi = -lambda*K_G phi.
    ///
    /// METHOD: block inverse (subspace) iteration, Bathe-style:
    ///   1. iterate y_j = K^-1 * (-K_G * v_j) for every vector in the block
    ///      (CgSolver does the K^-1 apply; K is singular by 3 rigid modes,
    ///      so every vector and every CG right-hand side is projected onto
    ///      their complement first, exactly like CgSolver already does for
    ///      its own residual);
    ///   2. Gram-Schmidt orthonormalize the block;
    ///   3. Rayleigh-Ritz: project both K and -K_G onto the block (small
    ///      m x m matrices) and solve THAT generalized eigenproblem exactly
    ///      via Cholesky + Jacobi (both dense, ~40 lines, fine at m &lt;= ~8);
    ///   4. replace the block with the Ritz vectors (sorted by ascending
    ///      lambda) and repeat.
    /// The block is m = requested modes + 2 vectors: the two extras give
    /// the iteration room to sort out near-degenerate modes without losing
    /// one of the ones actually asked for.
    ///
    /// WHY NOT PLAIN POWER ITERATION ON K^-1*(-K_G) DIRECTLY: under uniform
    /// compression -K_G is positive-semidefinite, and repeated K^-1
    /// application makes the block converge to the largest eigenvalues of
    /// that positive operator, i.e. exactly the smallest positive lambda:
    /// no shift needed. Mixed tension/compression can still put spurious
    /// large-magnitude negative-lambda directions in the block; the
    /// non-positive ones are filtered out in <see cref="ExtractModes"/>
    /// rather than chased by the iteration, which is why the block carries
    /// two spares.
    ///
    /// REAL-TIME / NO PER-TICK ALLOCATION: every work array here is sized
    /// once by <see cref="Reset"/> (on topology change or mode-count change)
    /// and reused. <see cref="Step"/> runs at most <see cref="MaxSweepsPerTick"/>
    /// sweeps and returns without publishing anything until Ritz values stop
    /// moving; the caller (StructuralSolver) keeps calling Step tick after
    /// tick and only reads modes out once it returns true, so the subspace is
    /// warm-started for free across ticks (the load changes smoothly, so
    /// after the first topology change only a sweep or two is normally
    /// needed). Sweep budgets are TICK counts, never wall-clock, and the
    /// only "randomness" is a fixed deterministic seed pattern (no RNG), so
    /// two runs over identical inputs produce bit-identical output: see
    /// BucklingTests.Analysis_IsBitDeterministic.
    ///
    /// SERVER-ONLY DECISION, CLIENT-SAFE TINT: the float FE solve is not
    /// guaranteed bit-identical across machines (different CPUs/JIT), so any
    /// THRESHOLD decision made from it (which load factor crossed 1, which
    /// blocks therefore break) must be made in exactly one place (the
    /// authoritative server) and broadcast as an event, never re-derived
    /// locally. That is what StructuralSolver.BuckledBlocks is: consume it
    /// only on the authority. Clients may read BlockStress.BucklingRatio (a
    /// continuous tint, not a decision) freely, because a client's own tint
    /// disagreeing slightly with another machine's tint is invisible, while
    /// a client independently deciding a block died is a desync.
    /// </summary>
    public sealed class BucklingAnalysis
    {
        /// <summary>Sweeps to run per Step call: the per-tick cost cap.</summary>
        public int MaxSweepsPerTick = 2;

        /// <summary>Ritz values are considered converged once every tracked
        /// lambda changes by less than this between sweeps (relative to its
        /// own magnitude).</summary>
        public float Tolerance = 2e-3f;

        /// <summary>Sweeps actually run by the most recent Step call:
        /// mirrors CgSolver.LastIterationCount.</summary>
        public int LastSweepCount { get; private set; }

        /// <summary>Tick index (as passed to Step, NOT a wall-clock time) at
        /// which the subspace last converged and modes were published.</summary>
        public int LastConvergedTick { get; private set; } = -1;

        readonly CgSolver _cg = new CgSolver();

        int _dof = -1;
        int _m;
        int _modeCount;
        int _sweepsSinceReset;

        /// <summary>Hard cap, in cumulative sweeps since the last Reset,
        /// after which Step force-publishes whatever the block currently
        /// holds even if the per-slot tolerance check has not settled:
        /// mirrors CgSolver, which also returns its best estimate at
        /// MaxIterations rather than guaranteeing true convergence. Without
        /// this, rare transient near-linear-dependence among the block's
        /// spare vectors (see the per-slot check below) could in principle
        /// starve publication indefinitely.</summary>
        public int ForceConvergeAfterSweeps = 250;

        /// <summary>Sweeps since Reset before Step will EVER report
        /// converged, tolerance check or force-cap alike. A freshly
        /// seeded block can pass through a few sweeps of transient
        /// near-linear-dependence (an accidental, temporary alignment of
        /// the fixed deterministic seed, not a real fixed point) that can
        /// otherwise look stable enough to satisfy Tolerance by
        /// coincidence; a small floor rides past it cheaply.</summary>
        public int MinSweepsBeforeConvergence = 30;

        /// <summary>True once some sweep since the last Reset has seen at
        /// least one nonzero mu: distinguishes "this block has never found
        /// any coupling yet" (legitimate: e.g. no compression anywhere, see
        /// <see cref="StuckSweepsBeforeReseed"/>'s doc) from "this block HAD
        /// real signal and then every slot went silent at once" (the
        /// collapse this fix targets). Only the latter is anomalous enough
        /// to justify discarding the warm-started subspace.</summary>
        bool _sawNonzeroMu;

        /// <summary>Consecutive sweeps every mu has read exactly 0 SINCE
        /// <see cref="_sawNonzeroMu"/> went true: see the class doc's Mono
        /// note on a divide by a near-zero Cholesky pivot occasionally
        /// landing on a merely-huge-but-finite value rather than the
        /// intended signed-infinity sentinel. That value then gets mixed by
        /// Jacobi's plane rotations into every slot, and once the resulting
        /// subspace vectors have collapsed this way (checked directly
        /// against a captured failure log) they are a genuine fixed point:
        /// Gram-Schmidt only rescales a vector whose norm is already
        /// informative (see Orthonormalize's `norm > 1e-8f` guard), so a
        /// collapsed all-mu-zero state reproduces itself identically every
        /// later sweep and no amount of further iteration escapes it. Left
        /// unchecked, the existing "value hasn't changed" convergence
        /// tolerance (see the per-slot loop below) would otherwise treat a
        /// collapsed all-Infinity lambda as trivially settled within just
        /// 2-3 more sweeps; that tolerance check is separately overridden
        /// (see its own doc, "post-signal all-mu-zero") so it can never
        /// publish while a collapse is still ambiguous, which is what buys
        /// this counter the room to use a patient threshold instead of
        /// racing that shortcut.</summary>
        int _stuckSweeps;

        /// <summary>Consecutive post-signal all-mu-zero sweeps (see
        /// <see cref="_stuckSweeps"/>) before Step gives up warm-starting
        /// from a collapsed subspace and reseeds it fresh instead. Gated on
        /// <see cref="_sawNonzeroMu"/> rather than a sweep-count floor (a
        /// genuinely-uncompressed block's spares can legitimately sit at
        /// mu=0 indefinitely, see ExtractModes' doc, but never after having
        /// shown real signal first). Deliberately patient (comfortably
        /// above the several sweeps a HEALTHY run can legitimately spend
        /// mid self-correction with every slot transiently silent, observed
        /// directly in this file's own test suite) now that the tolerance
        /// override below removes the time pressure to fire fast: a true
        /// collapse is a permanent fixed point (see this field's doc) and
        /// will still be sitting at all-mu-zero however long this waits, so
        /// there is no cost to giving genuine self-correction first crack.</summary>
        public int StuckSweepsBeforeReseed = 100;

        float[][] _v;      // current subspace block
        float[][] _rhs;    // -K_G * v_j, also the CG right-hand side
        float[][] _y;      // CG solutions / next Ritz vectors
        float[][] _kv;     // K * y_j (Rayleigh-Ritz)
        float[][] _kgv;    // -K_G * y_j (Rayleigh-Ritz)

        float[,] _kr, _gr;   // projected m x m matrices
        float[,] _l;         // Cholesky factor of _kr
        float[,] _mid;       // L^-1 * Gr * L^-T
        float[,] _eigVecs;   // Jacobi eigenvectors of _mid
        float[] _mu;         // Jacobi eigenvalues of _mid
        float[] _lambda;     // 1/mu, this sweep
        float[] _lambdaPrev; // 1/mu, previous sweep
        int[] _order;        // ascending-lambda permutation

        /// <summary>Last snapshot of _v/_lambda taken on a sweep that
        /// satisfied the ORDINARY tolerance check (i.e. NOT merely because
        /// ForceConvergeAfterSweeps ran out of patience). ExtractModes reads
        /// from this, never from the raw working _v/_lambda, so a force-
        /// publish never hands out a mid-oscillation value from whatever
        /// sweep happened to be in flight when the sweep cap fired: see
        /// TODO.md's note on the Mono-only mu oscillation this fixes, and
        /// _hasPublishedSnapshot's doc for the case where no such sweep has
        /// ever happened yet.</summary>
        float[][] _publishedV;
        float[] _publishedLambda;

        /// <summary>True once at least one sweep since the last Reset has
        /// satisfied the ordinary tolerance check and _publishedV/_publishedLambda
        /// hold a trustworthy snapshot. A force-publish before this is ever
        /// true has nothing stable to fall back on, so it does NOT publish
        /// (see the force-publish branch below): better to keep iterating a
        /// few more sweeps than to hand out an admittedly-unstable value.</summary>
        bool _hasPublishedSnapshot;

        /// <summary>True once the last Step call converged and
        /// <see cref="ExtractModes"/> is safe to call against the current
        /// subspace.</summary>
        public bool Converged { get; private set; }

        /// <summary>
        /// (Re)allocates every work array for `dof` degrees of freedom and a
        /// block of `modeCount` + 2 vectors, and reseeds the subspace with a
        /// fixed deterministic pattern (never a RNG; see the class doc on
        /// determinism). Call whenever the topology (dof count) or the
        /// requested mode count changes; StructuralSolver also calls this
        /// when compression disappears, so a later reappearance starts clean
        /// rather than warm-starting from a stale, unrelated subspace.
        /// </summary>
        public void Reset(int dof, int modeCount, float[][] rigidModes)
        {
            int m = modeCount + 2;
            _modeCount = modeCount;
            if (_dof != dof || _m != m)
            {
                _dof = dof;
                _m = m;
                _v = Alloc(m, dof);
                _rhs = Alloc(m, dof);
                _y = Alloc(m, dof);
                _kv = Alloc(m, dof);
                _kgv = Alloc(m, dof);
                _kr = new float[m, m];
                _gr = new float[m, m];
                _l = new float[m, m];
                _mid = new float[m, m];
                _eigVecs = new float[m, m];
                _mu = new float[m];
                _lambda = new float[m];
                _lambdaPrev = new float[m];
                _order = new int[m];
                _publishedV = Alloc(m, dof);
                _publishedLambda = new float[m];
            }

            SeedBlock(rigidModes, 0);

            // NaN, not +Infinity: a genuinely converged (near-zero mu)
            // slot can legitimately report lambda = +Infinity (see
            // SolveGeneralizedEigen), and comparing that against an
            // "unset" sentinel of the SAME value would spuriously call
            // sweep 0 converged. NaN never equals anything, including
            // itself, so the check below always treats it as unset.
            for (int i = 0; i < _m; i++) _lambdaPrev[i] = float.NaN;
            _sweepsSinceReset = 0;
            _stuckSweeps = 0;
            _sawNonzeroMu = false;
            _hasPublishedSnapshot = false;
            Converged = false;
            LastSweepCount = 0;
            LastConvergedTick = -1;
        }

        /// <summary>Fills every slot of the current subspace block with the
        /// same fixed deterministic seed pattern Reset uses (a sum of a few
        /// incommensurate sinusoids per vector, never an RNG, which is what
        /// the bit-determinism test depends on), salted by `salt` so a
        /// mid-run reseed (see Step's stuck-subspace doc) starts from a
        /// genuinely different point than whatever seed produced the stuck
        /// state, while staying just as deterministic as sweep 0's seed for
        /// the same `salt`.</summary>
        void SeedBlock(float[][] rigidModes, int salt)
        {
            for (int k = 0; k < _m; k++)
            {
                var vk = _v[k];
                float freq = 1.3f + 0.7f * k + 0.11f * salt;
                for (int i = 0; i < _dof; i++)
                    vk[i] = (float)Math.Sin(freq * (i + 1) * 0.6180339887f + k + salt);
            }

            foreach (var vk in _v)
                CgSolver.Project(vk, rigidModes);
            CgSolver.Orthonormalize(_v);
        }

        static float[][] Alloc(int m, int dof)
        {
            var a = new float[m][];
            for (int i = 0; i < m; i++) a[i] = new float[dof];
            return a;
        }

        /// <summary>
        /// Runs up to <see cref="MaxSweepsPerTick"/> subspace-iteration
        /// sweeps, warm-started from wherever the previous call left off.
        /// Returns true (and sets <see cref="Converged"/>) once the tracked
        /// Ritz values stop moving, at which point <paramref name="tick"/>
        /// is recorded in <see cref="LastConvergedTick"/> and
        /// <see cref="ExtractModes"/> is safe to call.
        /// </summary>
        public bool Step(StiffnessAssembly k, GeometricStiffness kg, float[][] rigidModes, int tick)
        {
            int sweeps = 0;
            bool converged = false;

            for (int iter = 0; iter < MaxSweepsPerTick; iter++)
            {
                sweeps++;
                _sweepsSinceReset++;
                for (int j = 0; j < _m; j++)
                {
                    kg.Multiply(_v[j], _rhs[j]);
                    for (int i = 0; i < _dof; i++) _rhs[j][i] = -_rhs[j][i];
                    CgSolver.Project(_rhs[j], rigidModes);

                    // Warm-start CG from the current subspace vector: the
                    // whole point of carrying _v across sweeps/ticks.
                    Array.Copy(_v[j], _y[j], _dof);
                    _cg.Solve(k, _rhs[j], _y[j], rigidModes);
                }

                CgSolver.Orthonormalize(_y);

                for (int j = 0; j < _m; j++)
                {
                    k.Multiply(_y[j], _kv[j]);
                    kg.Multiply(_y[j], _kgv[j]);
                    for (int i = 0; i < _dof; i++) _kgv[j][i] = -_kgv[j][i];
                }

                for (int a = 0; a < _m; a++)
                for (int b = 0; b < _m; b++)
                {
                    _kr[a, b] = CgSolver.Dot(_y[a], _kv[b]);
                    _gr[a, b] = CgSolver.Dot(_y[a], _kgv[b]);
                }
                // Symmetrize away rounding asymmetry before Cholesky/Jacobi,
                // which both assume exact symmetry.
                Symmetrize(_kr, _m);
                Symmetrize(_gr, _m);

                SolveGeneralizedEigen();

                // STUCK-SUBSPACE DETECTION AND RESEED: see _sawNonzeroMu's
                // and _stuckSweeps' docs for why "every slot suddenly reads
                // exactly 0, having shown real signal before" is a genuine
                // fixed point, not a transient to iterate past. The ordinary
                // tolerance-based convergence check below is separately
                // overridden (see its own "post-signal all-mu-zero" doc) so
                // it can never mistake a frozen all-Infinity lambda for a
                // settled answer while this counter is still accumulating,
                // which is what lets StuckSweepsBeforeReseed stay patient
                // instead of racing that check.
                bool allZero = true;
                for (int i = 0; i < _m; i++)
                {
                    if (_mu[i] != 0f) { allZero = false; break; }
                }
                if (!allZero) _sawNonzeroMu = true;

                _stuckSweeps = (allZero && _sawNonzeroMu) ? _stuckSweeps + 1 : 0;
                if (_stuckSweeps >= StuckSweepsBeforeReseed)
                {
                    // Salted by _sweepsSinceReset so the fresh seed is not
                    // just the sweep-0 seed the stuck state may itself have
                    // originated from repeating.
                    SeedBlock(rigidModes, _sweepsSinceReset);
                    for (int i = 0; i < _m; i++) _lambdaPrev[i] = float.NaN;
                    _stuckSweeps = 0;
                    _sawNonzeroMu = false;
                    converged = false;
                    continue;
                }

                for (int a = 0; a < _m; a++) _order[a] = a;
                Array.Sort(_order, (p, q) => _lambda[p].CompareTo(_lambda[q]));

                // New subspace vectors are the Ritz combinations of _y,
                // reordered by ascending lambda, written into _v via _rhs
                // as scratch so we never read a _v slot we are about to
                // overwrite (Rayleigh-Ritz combines ALL of _y into EVERY
                // new vector).
                for (int outIdx = 0; outIdx < _m; outIdx++)
                {
                    int src = _order[outIdx];
                    var dst = _rhs[outIdx];
                    Array.Clear(dst, 0, _dof);
                    for (int j = 0; j < _m; j++)
                    {
                        float c = _eigVecs[j, src];
                        if (c == 0f) continue;
                        var yj = _y[j];
                        for (int i = 0; i < _dof; i++) dst[i] += c * yj[i];
                    }
                }
                for (int outIdx = 0; outIdx < _m; outIdx++)
                    Array.Copy(_rhs[outIdx], _v[outIdx], _dof);

                var sortedLambda = new float[_m];
                for (int outIdx = 0; outIdx < _m; outIdx++)
                    sortedLambda[outIdx] = _lambda[_order[outIdx]];

                // _lambda ITSELF must move to the same ascending-lambda order
                // as _v, not just this local sortedLambda copy: ExtractModes
                // reads the class fields _lambda[idx] and _v[idx] together as
                // ONE mode (see its doc), and _v above was just permuted by
                // _order while _lambda (last written by SolveGeneralizedEigen
                // in raw, unsorted Jacobi-output order) was not. Near a
                // settled convergence that raw order usually already
                // coincides with ascending order (the subspace feeding
                // Jacobi is itself already close to sorted from the previous
                // sweep), which is what let this silently work by
                // coincidence; ANY sweep where Jacobi's raw order is not
                // already sorted hands ExtractModes a shape/load-factor pair
                // for two DIFFERENT modes. Copying the already-computed
                // sortedLambda over _lambda keeps the two arrays in lockstep
                // unconditionally, independent of whether this sweep's raw
                // order happened to be trivial.
                Array.Copy(sortedLambda, _lambda, _m);

                // Only the requested modeCount smallest-lambda slots need to
                // settle for the analysis to be USABLE: the 2 spares exist
                // purely to give the iteration room and can wander (or sit at
                // a degenerate near-mu-zero value) indefinitely without that
                // ever meaning the requested modes have not converged.
                // Genuinely meaningless tracked values (near-zero or
                // infinite lambda, "no coupling on this Ritz direction
                // yet") are NOT specially rejected here: CgSolver's own
                // tolerance puts a noise floor under Tolerance's precision,
                // so demanding every tracked slot be simultaneously stable
                // AND finite AND non-negligible before ever declaring
                // convergence can starve on that noise indefinitely.
                // ExtractModes is the actual gate against publishing a
                // meaningless value: it drops non-positive/non-finite
                // lambda outright, so a spurious transient here just yields
                // fewer modes THIS tick, corrected the moment real
                // compressive signal (which RunBuckling has already
                // confirmed exists) develops on a later one.
                converged = true;
                int tracked = Math.Min(_modeCount, _m);
                for (int i = 0; i < tracked; i++)
                {
                    float prev = _lambdaPrev[i];
                    float cur = sortedLambda[i];
                    if (float.IsNaN(prev))
                    {
                        // Sweep 0 for this slot: no history to compare yet.
                        converged = false;
                    }
                    else if (float.IsInfinity(prev) || float.IsInfinity(cur))
                    {
                        if (prev != cur) converged = false;
                    }
                    else
                    {
                        float scale = Math.Max(1e-6f, Math.Abs(prev));
                        if (Math.Abs(cur - prev) > Tolerance * scale) converged = false;
                    }
                }
                Array.Copy(sortedLambda, _lambdaPrev, _m);

                // Do NOT let a post-signal all-mu-zero sweep (see the
                // stuck-subspace doc above) satisfy convergence via the
                // IsInfinity "value hasn't changed" branch just above: that
                // branch exists for a legitimately, PERSISTENTLY
                // uncompressed block (_sawNonzeroMu false the whole run),
                // not for a subspace that just collapsed FROM real signal,
                // which can otherwise look "stable" (frozen at the same
                // Infinity) within 2-3 sweeps of collapsing -- far sooner
                // than _stuckSweeps could ever reach StuckSweepsBeforeReseed
                // and actually recover it. This keeps the sweep budget
                // running (rather than falsely publishing) until either the
                // block self-corrects on its own (allZero goes false again,
                // exactly as happens routinely in a healthy run) or the
                // reseed above gets its chance to fire.
                if (allZero && _sawNonzeroMu) converged = false;

                if (_sweepsSinceReset < MinSweepsBeforeConvergence) converged = false;

                // PUBLISHED SNAPSHOT: only a sweep that satisfies the
                // ordinary tolerance check (converged, at this point) is
                // trustworthy enough for ExtractModes to read; see
                // _publishedV's doc and TODO.md's Mono note on why a
                // force-publish must never hand out whichever sweep
                // happened to be in flight when ForceConvergeAfterSweeps
                // fired instead.
                if (converged)
                {
                    for (int i = 0; i < _m; i++) Array.Copy(_v[i], _publishedV[i], _dof);
                    Array.Copy(_lambda, _publishedLambda, _m);
                    _hasPublishedSnapshot = true;
                }
                else if (_sweepsSinceReset >= ForceConvergeAfterSweeps)
                {
                    // Force-publish, but only the last snapshot that was
                    // actually stable: if none exists yet, there is nothing
                    // safe to hand out, so this keeps iterating instead
                    // (see ForceConvergeAfterSweeps' doc).
                    converged = _hasPublishedSnapshot;
                }

                if (converged) break;
            }

            LastSweepCount = sweeps;
            Converged = converged;
            if (converged) LastConvergedTick = tick;
            return converged;
        }

        static void Symmetrize(float[,] a, int n)
        {
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float avg = 0.5f * (a[i, j] + a[j, i]);
                a[i, j] = avg;
                a[j, i] = avg;
            }
        }

        /// <summary>
        /// Solves the small projected generalized eigenproblem _gr*c =
        /// mu*_kr*c (mu approximates an eigenvalue of A = K^-1*(-K_G)) via
        /// Cholesky(_kr) = L L^T, reduction to the standard symmetric problem
        /// M = L^-1 * _gr * L^-T, and cyclic Jacobi on M. Writes
        /// <see cref="_lambda"/> = 1/mu (see the class doc for the
        /// K phi = -lambda*K_G phi sign) and <see cref="_eigVecs"/> = L^-T *
        /// (Jacobi eigenvectors), the coefficients back in terms of the
        /// ORIGINAL block _y.
        /// </summary>
        void SolveGeneralizedEigen()
        {
            Cholesky(_kr, _m, _l);

            // temp = L^-1 * _gr (solve L * temp = _gr, column by column).
            var temp = _mid; // reuse as scratch before it holds the final M
            for (int col = 0; col < _m; col++)
            {
                for (int i = 0; i < _m; i++)
                {
                    float sum = _gr[i, col];
                    for (int k = 0; k < i; k++) sum -= _l[i, k] * temp[k, col];
                    temp[i, col] = sum / _l[i, i];
                }
            }

            // M = temp * L^-T, i.e. solve L * M^T = temp^T row by row of M.
            var m2 = new float[_m, _m];
            for (int row = 0; row < _m; row++)
            {
                for (int i = 0; i < _m; i++)
                {
                    float sum = temp[row, i];
                    for (int k = 0; k < i; k++) sum -= _l[i, k] * m2[row, k];
                    m2[row, i] = sum / _l[i, i];
                }
            }
            Symmetrize(m2, _m);
            Array.Copy(m2, _mid, m2.Length);

            // The block carries 2 spare vectors beyond the requested mode
            // count (see the class doc), and under weak or near-zero stress
            // (little/no real compression anywhere) those spares have
            // nothing left to converge toward: Gram-Schmidt normalizes them
            // out of near-pure numerical noise, Kr picks up a near-zero
            // diagonal entry for that slot, and dividing Gr through by the
            // resulting near-zero Cholesky pivot (twice, once per L^-1 and
            // once per L^-T) can overflow to +-Infinity. Sanitize rather than
            // chase the ill-conditioning: a spare slot going non-finite here
            // carries no information the analysis needs (a genuine, well-
            // conditioned mode never produces one), so it is neutralized to
            // 0, and Jacobi (which cannot handle non-finite input) never
            // sees it.
            for (int i = 0; i < _m; i++)
            for (int j = 0; j < _m; j++)
                if (!float.IsFinite(_mid[i, j])) _mid[i, j] = 0f;

            DenseJacobiEigen.Solve(_mid, _m, _mu, _eigVecs);

            // c_i = L^-T * w_i (back-substitute L^T c = w for each column).
            var coeffs = new float[_m, _m];
            for (int col = 0; col < _m; col++)
            {
                for (int i = _m - 1; i >= 0; i--)
                {
                    float sum = _eigVecs[i, col];
                    for (int k = i + 1; k < _m; k++) sum -= _l[k, i] * coeffs[k, col];
                    coeffs[i, col] = sum / _l[i, i];
                }
            }
            Array.Copy(coeffs, _eigVecs, coeffs.Length);

            // mu are the Ritz values of A = K^-1*(-K_G) restricted to this
            // subspace (Gr c = mu*Kr c, i.e. the K-inner-product Rayleigh
            // quotient of A: see the class doc for why plain power
            // iteration on A finds the smallest positive lambda first under
            // uniform compression). A phi = mu*phi with A = K^-1*(-K_G) means
            // -K_G phi = mu*K phi, i.e. K phi = -(1/mu)*K_G phi, so
            // lambda = 1/mu, NOT -mu. mu ~ 0 (or negative) means no
            // meaningful positive lambda along that Ritz direction; represent
            // it as a signed infinity so it sorts to the correct end and gets
            // filtered out by ExtractModes without a divide blowing up into
            // a merely-large finite number that could masquerade as a mode.
            for (int i = 0; i < _m; i++)
            {
                float mu = _mu[i];
                _lambda[i] = Math.Abs(mu) > 1e-9f
                    ? 1f / mu
                    : (mu >= 0f ? float.PositiveInfinity : float.NegativeInfinity);
            }
        }

        /// <summary>Lower-triangular Cholesky, a = l*l^T. `a` is assumed SPD
        /// (true here: _kr is K projected onto a subspace already clear of
        /// the rigid modes, and K is positive definite off them). A tiny
        /// diagonal floor guards only against benign rounding, not a real
        /// indefinite input: an indefinite _kr would be a programmer bug.</summary>
        static void Cholesky(float[,] a, int n, float[,] l)
        {
            for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                float sum = a[i, j];
                for (int k = 0; k < j; k++) sum -= l[i, k] * l[j, k];
                if (i == j)
                    l[i, j] = (float)Math.Sqrt(Math.Max(sum, 1e-12f));
                else
                    l[i, j] = sum / l[j, j];
            }
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                l[i, j] = 0f;
        }


        /// <summary>
        /// Reads the requested number of positive, sub-threshold-safe modes
        /// out of the converged subspace (call only when <see cref="Step"/>
        /// last returned true). Non-positive lambda (tension-stabilized or
        /// numerical rigid leakage) are skipped, not returned, per the task's
        /// buckling definition. Blocks are visited in ascending key order so
        /// the resulting BlockParticipation dictionaries are built
        /// deterministically (their contents do not depend on iteration
        /// order, but the summation that produces the floats does).
        /// </summary>
        public List<BucklingMode> ExtractModes(Hullbreach.Core.BlockGrid grid, StiffnessAssembly assembly, int modeCount)
        {
            var result = new List<BucklingMode>();

            var keys = grid.All.Select(kvp => kvp.Key).ToList();
            keys.Sort();

            float h = Hullbreach.Core.BlockType.Width;
            var nodeIds = new int[NodeLattice.NodesPerElement];
            var dofs = new int[Q8Element.DofCount];
            var ue = new float[Q8Element.DofCount];

            for (int idx = 0; idx < _m && result.Count < modeCount; idx++)
            {
                float lambda = _publishedLambda[idx];
                // Skip non-positive lambda (tension-stabilized / rigid
                // leakage, see the class doc) AND non-finite ones: +Infinity
                // means "no coupling found on this Ritz direction" (mu ~ 0),
                // which ForceConvergeAfterSweeps can still hand back for a
                // slot that has not developed real signal yet: that is not
                // a mode, it is an empty seat, and reporting it as one with
                // an infinite load factor would be actively misleading.
                if (!(lambda > 1e-6f) || float.IsInfinity(lambda)) continue;

                var shape = (float[])_publishedV[idx].Clone();
                float maxAbs = 0f;
                for (int i = 0; i < _dof; i++) maxAbs = Math.Max(maxAbs, Math.Abs(shape[i]));
                if (maxAbs > 1e-12f)
                    for (int i = 0; i < _dof; i++) shape[i] /= maxAbs;

                var participation = new Dictionary<int, float>();
                var energies = new List<(int key, float energy)>();
                float total = 0f;

                foreach (var key in keys)
                {
                    Hullbreach.Core.BlockKey.Unpack(key, out int x, out int y);
                    NodeLattice.NodesOf(x, y, nodeIds);
                    for (int i = 0; i < NodeLattice.NodesPerElement; i++)
                    {
                        int dense = assembly.NodeMap[nodeIds[i]];
                        dofs[2 * i] = 2 * dense;
                        dofs[2 * i + 1] = 2 * dense + 1;
                    }
                    for (int i = 0; i < Q8Element.DofCount; i++) ue[i] = shape[dofs[i]];

                    grid.TryGet(key, out var block);
                    float e = Hullbreach.Core.BlockTypes.EffectiveStiffness(block);
                    byte poissonClass = Hullbreach.Core.BlockTypes.Get(block.TypeId).PoissonClass;
                    var kHat = Q8Element.KHatFor(poissonClass, h);

                    float energy = 0f;
                    for (int i = 0; i < Q8Element.DofCount; i++)
                    {
                        float rowSum = 0f;
                        for (int j = 0; j < Q8Element.DofCount; j++)
                            rowSum += kHat[i, j] * ue[j];
                        energy += ue[i] * rowSum;
                    }
                    energy *= e;
                    energy = Math.Max(0f, energy);

                    energies.Add((key, energy));
                    total += energy;
                }

                if (total > 1e-12f)
                {
                    foreach (var (key, energy) in energies)
                    {
                        float frac = energy / total;
                        if (frac > 1e-4f) participation[key] = frac;
                    }
                }

                result.Add(new BucklingMode
                {
                    LoadFactor = lambda,
                    Shape = shape,
                    BlockParticipation = participation,
                });
            }

            return result;
        }
    }
}
