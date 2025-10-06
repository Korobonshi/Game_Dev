using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyAI: Easy/Medium/Hard (Hard = climb mapping bertingkat).
/// - Hard: GRID SCAN WINDOW (multi-probe) ke atas + snap to top + tepi aman
/// - Adaptive re-planning: rebuild rute saat player pindah/LOS jelas/stuck
/// - Jump kontekstual (tanpa normalized), default kalem
/// - GIZMOS: visualisasi semua ray scan (hijau=hit, merah=miss)
/// - Anti-overshoot: batasi pemilihan platform terlalu tinggi dari player; prefer di bawah player
/// </summary>
public class EnemyAI : Enemy
{
    public enum Difficulty { Easy, Medium, Hard, Impossible }

    [Header("Difficulty")]
    public Difficulty difficulty = Difficulty.Hard;

    [Header("Target")]
    public Transform player;
    [SerializeField] private GameObject foundPlayer;

    [Header("Movement")]
    public float chaseSpeed = 2.4f;

    [Header("Jump")]
    public float jumpForce = 6.5f;                     // impuls dasar
    [Range(0f, 1.5f)] public float jumpHorizontalBias = 0.4f;
    [Range(0.7f, 1.2f)] public float jumpVerticalScale = 0.95f;
    [Range(0f, 2f)] public float jumpHorizontalScale = 1.0f;
    public float jumpCooldown = 0.35f;                 // anti-spam
    public float contextualJumpYBoost = 1.0f;          // booster tinggi saat perlu

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask platformLayer;

    [Header("Combat")]
    public float attackRange = 0.6f;
    public float attackCooldown = 0.8f;

    [Header("Raycast Checks")]
    public float groundCheckRadius = 0.14f;
    public Vector2 groundCheckOffset = new Vector2(0f, -0.45f);
    public float frontCheckDist = 1.4f;
    public float gapCheckDownDist = 2.1f;
    public float overheadCheck = 3.0f;
    public float playerAboveThreshold = 0.8f;

    [Header("Hard Climb Mapping (Edges)")]
    public float highPlatformDeltaY = 2.8f; // selisih tinggi untuk memicu climb
    public float climbProbeStepY = 2.0f;
    public float climbProbeHoriz = 0.8f;    // offset tepi kiri/kanan
    public float climbLedgeMargin = 0.25f;  // mundur dari tepi saat mendarat
    public int   climbMaxSteps = 6;
    public float waypointReachDist = 0.35f;

    [Header("Hard Scan Window (anti-stuck)")]
    public float climbScanStepY = 0.6f;     // langkah vertikal kecil
    public int   climbScanMaxSteps = 12;    // banyak level Y yang discan
    public float climbScanSweepX = 1.4f;    // lebar sapuan horizontal dari tepi
    public int   climbScanSamplesX = 5;     // sampel X per level (>=3)

    [Header("Climb Targeting")]
    [Tooltip("Seberapa tinggi di atas player yang masih boleh dipilih (meter).")]
    public float maxPlatformOvershootY = 0.6f;
    [Tooltip("Jika ada banyak kandidat di bawah player, pilih yang paling dekat tinggi player.")]
    public bool preferClosestBelowPlayer = true;

    [Header("Climb Robustness")]
    [SerializeField] private float skin = 0.06f;
    [SerializeField] private float headClearance = 0.25f;

    [Header("Hard Adaptive Replan")]
    public float replanDeltaY = 1.1f;
    public float replanDeltaX = 1.8f;
    public float replanCooldown = 0.55f;
    public float stuckWindow = 1.0f;
    public float stuckImproveMin = 0.15f;
    public bool  cancelClimbOnClearLOS = true;

    [Header("Impossible Teleport")]
    [Tooltip("Time between teleport attempts (seconds)")]
    public float teleportCooldown = 5.0f;
    [Tooltip("Preparation time before teleporting (seconds) - gives player time to dodge")]
    public float teleportPrepTime = 0.5f;
    [Tooltip("How often to update player's last known position (seconds)")]
    public float playerTrackingRate = 0.1f;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool shouldJump;
    private float nextAttackTime;
    private float nextJumpTime;
    private int dir = 1;

    // climb state
    private readonly List<Vector2> climbWaypoints = new();
    private readonly HashSet<Collider2D> visitedPlatforms = new();
    private int  climbIndex = 0;
    private bool climbing = false;

    // adaptive replan state
    private Vector2 routePlayerAnchor;
    private float   nextReplanTime = 0f;
    private float   stuckTimer = 0f;
    private float   lastDistToWP = Mathf.Infinity;

    // teleport state (Impossible difficulty)
    private bool isTeleporting = false;
    private bool isPreparingTeleport = false;
    private float nextTeleportTime = 0f;
    private float teleportPrepEndTime = 0f;
    private Vector2 playerLastKnownPosition;
    private float nextPlayerTrackTime = 0f;
    private Vector2 teleportTargetPosition;

    // === DEBUG SCAN CACHE ===
    private readonly List<(Vector2 origin, Vector2 end, bool hit)> scanDebugLines = new();

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!player)
        {
            foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer) player = foundPlayer.transform;
            else Debug.LogWarning("Player tidak ditemukan");
        }
        ApplyDifficultyPreset();

        // physics defaults agar impuls terasa
        if (rb)
        {
            rb.linearDamping = Mathf.Min(rb.linearDamping, 0.1f);
            if (rb.mass < 0.5f) rb.mass = 1f;
        }
    }

    private void ApplyDifficultyPreset()
    {
        switch (difficulty)
        {
            case Difficulty.Easy:
                chaseSpeed = Mathf.Max(chaseSpeed, 1.6f);
                attackCooldown = 1.2f;
                frontCheckDist = 1.0f;
                gapCheckDownDist = 1.6f;
                overheadCheck = 2.0f;
                jumpForce = Mathf.Max(jumpForce, 5f);
                break;
            case Difficulty.Medium:
                chaseSpeed = Mathf.Max(chaseSpeed, 2.2f);
                attackCooldown = 0.95f;
                frontCheckDist = 1.3f;
                gapCheckDownDist = 2.0f;
                overheadCheck = 2.6f;
                jumpForce = Mathf.Max(jumpForce, 6f);
                break;
            case Difficulty.Hard:
                chaseSpeed = Mathf.Max(chaseSpeed, 2.8f);
                attackCooldown = 0.75f;
                frontCheckDist = 1.6f;
                gapCheckDownDist = 2.4f;
                overheadCheck = 3.2f;
                jumpForce = Mathf.Max(jumpForce, 6.5f);
                highPlatformDeltaY = Mathf.Max(highPlatformDeltaY, 2.5f);
                break;
            case Difficulty.Impossible:
                // Same stats as Hard but with teleportation
                chaseSpeed = Mathf.Max(chaseSpeed, 3.2f); // Slightly faster
                attackCooldown = 0.65f; // Slightly more aggressive
                frontCheckDist = 1.6f;
                gapCheckDownDist = 2.4f;
                overheadCheck = 3.2f;
                jumpForce = Mathf.Max(jumpForce, 7f);
                highPlatformDeltaY = Mathf.Max(highPlatformDeltaY, 2.5f);
                // Initialize teleport timing
                nextTeleportTime = Time.time + teleportCooldown;
                nextPlayerTrackTime = Time.time + playerTrackingRate;
                break;
        }
        jumpHorizontalBias = Mathf.Clamp(jumpHorizontalBias, 0.3f, 0.6f);
        jumpVerticalScale  = Mathf.Clamp(jumpVerticalScale, 0.85f, 1.0f);
        jumpCooldown       = Mathf.Clamp(jumpCooldown, 0.25f, 0.5f);
    }

    private void Update() => Move();

    public override void Move()
    {
        if (!player) return;

        dir = player.position.x >= transform.position.x ? 1 : -1;

        // Grounded?
        Vector2 gc = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapCircle(gc, groundCheckRadius, groundLayer | platformLayer);

        // Ray dasar
        Vector2 origin = transform.position;
        RaycastHit2D groundInFront = Physics2D.Raycast(origin, new Vector2(dir, 0f), frontCheckDist, groundLayer);
        Vector2 gapOrigin = origin + new Vector2(dir * frontCheckDist, 0f);
        RaycastHit2D gapAhead = Physics2D.Raycast(gapOrigin, Vector2.down, gapCheckDownDist, groundLayer);
        RaycastHit2D platformAbove = Physics2D.Raycast(origin, Vector2.up, overheadCheck, platformLayer);
        bool isPlayerAbove = (player.position.y - transform.position.y) > playerAboveThreshold;

        // Gerak horizontal dasar (disabled during teleport)
        if (isGrounded && !climbing && !isTeleporting && !isPreparingTeleport)
            rb.linearVelocity = new Vector2(dir * chaseSpeed, rb.linearVelocity.y);

        // Keputusan lompat kalem (disabled during teleport)
        shouldJump = false;
        if (isGrounded && !climbing && !isTeleporting && !isPreparingTeleport && Time.time >= nextJumpTime)
        {
            if (!gapAhead.collider && !groundInFront.collider) shouldJump = true;   // tepi/jurang
            else if (groundInFront.collider)                    shouldJump = true;   // dinding
            else if (isPlayerAbove && platformAbove.collider)   shouldJump = true;   // platform atas
        }

        // Hard/Impossible: trigger climb bila player jauh di atas
        if (difficulty == Difficulty.Hard || difficulty == Difficulty.Impossible)
        {
            float deltaY = player.position.y - transform.position.y;
            if (deltaY >= highPlatformDeltaY && !climbing && !isTeleporting)
            {
                if (BuildClimbRoute())
                {
                    climbing = true; climbIndex = 0;
                    routePlayerAnchor = player.position;
                    ResetStuckWatch();
                }
            }

            if (climbing) AdaptiveReplanChecks();
        }

        // Impossible: Handle teleportation mechanics
        if (difficulty == Difficulty.Impossible)
        {
            HandleTeleportMechanics();
        }

        if (climbing && !isTeleporting) FollowClimbRoute();

        // Serang
        if (Vector2.Distance(transform.position, player.position) <= attackRange
            && Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }

        // Debug (runtime)
        DebugDrawRays(origin, groundInFront, gapOrigin, gapAhead, platformAbove);
    }

    private void FixedUpdate()
    {
        if (isGrounded && shouldJump && !climbing && !isTeleporting && !isPreparingTeleport && Time.time >= nextJumpTime)
        {
            shouldJump = false;
            float needDy = Mathf.Max(0f, player.position.y - transform.position.y);
            DoJump(dir, needDy);
        }
    }

    // =======================
    // ===== JUMP LOGIC  =====
    // =======================
    private void DoJump(int moveDir, float targetDy = 0f)
    {
        // TANPA .normalized → vertikal tidak mengecil
        float hx = Mathf.Sign(moveDir) * jumpHorizontalBias;
        float impulseX = hx * jumpForce * jumpHorizontalScale;

        float calmY = jumpForce * jumpVerticalScale;

        // vNeeded ~ sqrt(2 g h) → impuls = v * mass
        float g = Mathf.Abs(Physics2D.gravity.y * (rb ? rb.gravityScale : 1f));
        float vNeeded = Mathf.Sqrt(Mathf.Max(0.01f, 2f * g * targetDy));
        float neededImpulseY = vNeeded * (rb ? rb.mass : 1f);

        float impulseY = Mathf.Max(calmY, neededImpulseY * contextualJumpYBoost);

        rb.AddForce(new Vector2(impulseX, impulseY), ForceMode2D.Impulse);
        nextJumpTime = Time.time + jumpCooldown;
    }

    // =======================
    // ===== CLIMB LOGIC =====
    // =======================

    // Snap ke top permukaan platform dgn ray turun dari atas
    private bool SnapToPlatformTop(Vector2 approxPos, out Vector2 topPoint, out Collider2D col)
    {
        Vector2 origin = approxPos + Vector2.up * (skin + headClearance);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, headClearance + 1.2f, platformLayer);
        if (hit.collider)
        {
            topPoint = hit.point + Vector2.up * (skin + 0.05f);
            col = hit.collider;
            return true;
        }
        topPoint = approxPos; col = null; return false;
    }

    // Pilih tepi mendarat aman di platform yang ditemukan (arah ke player)
    private bool FindSafeLandingEdge(RaycastHit2D platHit, int towardPlayer, out Vector2 landing, out Collider2D col)
    {
        Vector2 probe = platHit.point + new Vector2(towardPlayer * climbProbeHoriz, 0f);
        if (SnapToPlatformTop(probe, out landing, out col))
        {
            landing += new Vector2(-towardPlayer * climbLedgeMargin, 0f);
            return true;
        }
        probe = platHit.point + new Vector2(-towardPlayer * climbProbeHoriz, 0f);
        if (SnapToPlatformTop(probe, out landing, out col))
        {
            landing += new Vector2(towardPlayer * climbLedgeMargin, 0f);
            return true;
        }
        landing = platHit.point; col = null; return false;
    }

    // GRID SCAN WINDOW: multi-probe ke atas (tiap level Y menembak ray "turun") + anti-overshoot
    private bool ProbePlatformGrid(Vector2 from, int towardPlayer, out RaycastHit2D bestHit)
    {
        bool foundAny = false;
        bestHit = default;

        scanDebugLines.Clear(); // reset cache tiap scan

        int samples = Mathf.Max(1, climbScanSamplesX);
        float sweep = Mathf.Max(0.1f, climbScanSweepX);
        float stepY = Mathf.Max(0.2f, climbScanStepY);

        float playerY = player ? player.position.y : from.y + 5f;
        float capY    = playerY + Mathf.Max(0f, maxPlatformOvershootY);

        // Dua kandidat: bawah/tepat player & atas namun <= cap
        bool hasUnder = false, hasOver = false;
        RaycastHit2D bestUnder = default, bestOver = default;
        float bestUnderDelta = float.PositiveInfinity; // |playerY - y| minimal (bawah)
        float bestOverDelta  = float.PositiveInfinity; // (y - playerY) minimal (atas)

        for (int step = 0; step < climbScanMaxSteps; step++)
        {
            float y = from.y + (step + 1) * stepY;

            for (int s = 0; s < samples; s++)
            {
                // prioritas sisi ke arah player
                int idx = (towardPlayer >= 0) ? s : (samples - 1 - s);
                float t = samples == 1 ? 0.5f : (float)idx / (samples - 1);
                float xprio = Mathf.Lerp(-sweep, sweep, t);

                Vector2 origin = new Vector2(from.x + xprio + towardPlayer * climbProbeHoriz,
                                             y + headClearance + skin);

                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, headClearance + 1.0f, platformLayer);
                Vector2 end = origin + Vector2.down * (headClearance + 1.0f);

                // cache utk gizmo + runtime debug
                scanDebugLines.Add((origin, end, hit.collider));
                Debug.DrawLine(origin, end, hit.collider ? Color.green : Color.red, 0f, false);

                if (!hit.collider) continue;

                float hy = hit.point.y;

                // Abaikan yang melampaui cap (terlalu tinggi di atas player)
                if (hy > capY) continue;

                foundAny = true;

                if (hy <= playerY) // kandidat di bawah/tepat player
                {
                    float delta = Mathf.Abs(playerY - hy); // makin kecil makin baik
                    if (delta < bestUnderDelta)
                    {
                        bestUnderDelta = delta;
                        bestUnder = hit;
                        hasUnder = true;
                    }
                }
                else               // kandidat di atas player namun <= cap
                {
                    float delta = hy - playerY;           // makin kecil makin baik
                    if (delta < bestOverDelta)
                    {
                        bestOverDelta = delta;
                        bestOver = hit;
                        hasOver = true;
                    }
                }
            }

            // early-exit: kalau sudah dapat under yang sangat dekat (<= 0.1 m), cukup
            if (hasUnder && bestUnderDelta <= 0.1f) break;
        }

        // Aturan pilih akhir
        if (preferClosestBelowPlayer && hasUnder)
        {
            bestHit = bestUnder;
            return true;
        }
        if (hasOver)
        {
            bestHit = bestOver;
            return true;
        }
        if (!preferClosestBelowPlayer && hasUnder)
        {
            bestHit = bestUnder;
            return true;
        }

        bestHit = default;
        return foundAny; // false → tidak ada kandidat valid di jendela+cap
    }

    // Bangun rute waypoint ke atas platform menuju tinggi player (pakai grid scan + anti-overshoot)
    private bool BuildClimbRoute()
    {
        climbWaypoints.Clear();
        visitedPlatforms.Clear();

        Vector2 probePos = transform.position;
        if (SnapToPlatformTop(probePos, out var startTop, out var startCol))
        {
            probePos = startTop;
            if (startCol) visitedPlatforms.Add(startCol);
        }
        else probePos += Vector2.up * skin;

        int steps = 0, safety = 0;

        while (steps < climbMaxSteps && safety < climbMaxSteps * 6)
        {
            safety++;
            int towardPlayer = player.position.x >= probePos.x ? 1 : -1;

            // GRID SCAN WINDOW (anti-stuck + anti-overshoot)
            if (!ProbePlatformGrid(probePos, towardPlayer, out var platUp))
                break;

            if (visitedPlatforms.Contains(platUp.collider))
            {
                probePos = platUp.point + Vector2.up * (skin + 0.05f);
                continue;
            }

            if (!FindSafeLandingEdge(platUp, towardPlayer, out var landing, out var landingCol))
            {
                probePos = platUp.point + Vector2.up * (skin + 0.05f);
                continue;
            }

            // Hindari waypoint terlalu tinggi dari player (cap)
            float playerY = player ? player.position.y : landing.y;
            float capY = playerY + Mathf.Max(0f, maxPlatformOvershootY);
            if (landing.y > capY)
            {
                // skip & geser origin sedikit lalu lanjut
                probePos = new Vector2(landing.x, capY) + Vector2.up * (skin + 0.02f);
                continue;
            }

            if (landingCol) visitedPlatforms.Add(landingCol);

            climbWaypoints.Add(landing);
            probePos = landing + Vector2.up * (skin + 0.02f);
            steps++;

            if (landing.y >= playerY - 0.2f) break;
        }

        // Fallback sekali: perluas jendela jika kosong total
        if (climbWaypoints.Count == 0)
        {
            float oldSweep = climbScanSweepX;
            int   oldSamp  = climbScanSamplesX;
            float oldStepY = climbScanStepY;
            int   oldMax   = climbScanMaxSteps;

            climbScanSweepX  *= 1.4f;
            climbScanSamplesX = Mathf.Max(7, climbScanSamplesX + 2);
            climbScanStepY    = Mathf.Max(0.45f, climbScanStepY * 0.85f);
            climbScanMaxSteps = Mathf.Min(oldMax + 4, 18);

            int towardPlayer = player.position.x >= probePos.x ? 1 : -1;
            if (ProbePlatformGrid(probePos, towardPlayer, out var platUp2) &&
                FindSafeLandingEdge(platUp2, towardPlayer, out var landing2, out var col2))
            {
                float playerY = player ? player.position.y : landing2.y;
                float capY = playerY + Mathf.Max(0f, maxPlatformOvershootY);
                if (landing2.y <= capY)
                {
                    if (col2) visitedPlatforms.Add(col2);
                    climbWaypoints.Add(landing2);
                }
            }

            // pulihkan konfigurasi
            climbScanSweepX  = oldSweep;
            climbScanSamplesX = oldSamp;
            climbScanStepY    = oldStepY;
            climbScanMaxSteps = oldMax;
        }

        return climbWaypoints.Count > 0;
    }

    private void FollowClimbRoute()
    {
        if (climbIndex >= climbWaypoints.Count) { climbing = false; return; }

        Vector2 target = climbWaypoints[climbIndex];
        Vector2 delta  = target - (Vector2)transform.position;

        int moveDir = (delta.x >= 0f) ? 1 : -1;
        rb.linearVelocity = new Vector2(moveDir * Mathf.Max(chaseSpeed, 2.2f), rb.linearVelocity.y);

        // lompat kalem namun kontekstual bila butuh naik
        if (isGrounded && delta.y > 0.2f && Time.time >= nextJumpTime)
            DoJump(moveDir, Mathf.Max(0f, delta.y));

        if (delta.magnitude <= waypointReachDist)
        {
            climbIndex++;
            if (climbIndex >= climbWaypoints.Count) { climbing = false; return; }
            ResetStuckWatch();
        }

        WatchStuck(delta.magnitude);
        Debug.DrawLine(transform.position, target, Color.white);
    }

    // ==============================
    // ===== ADAPTIVE RE-PLANNING ===
    // ==============================
    private void AdaptiveReplanChecks()
    {
        if (Time.time < nextReplanTime) return;

        // 1) LOS ke player jelas? batalkan climb
        if (cancelClimbOnClearLOS && HasClearLOS((Vector2)transform.position, (Vector2)player.position))
        { climbing = false; return; }

        // 2) Player bergeser jauh dari anchor → replan
        Vector2 dp = (Vector2)player.position - routePlayerAnchor;
        if (Mathf.Abs(dp.y) >= replanDeltaY || Mathf.Abs(dp.x) >= replanDeltaX)
        { TryReplan(); return; }

        // 3) Waypoint terlalu rendah dibanding player → replan
        if (climbIndex < climbWaypoints.Count)
        {
            Vector2 wp = climbWaypoints[climbIndex];
            if (wp.y < player.position.y - 0.35f) { TryReplan(); return; }
        }
        else
        {
            if ((player.position.y - transform.position.y) > playerAboveThreshold) TryReplan();
        }
    }

    private bool HasClearLOS(Vector2 from, Vector2 to)
    {
        var hitPlat = Physics2D.Linecast(from + Vector2.up * 0.05f, to, platformLayer);
        return !hitPlat.collider;
    }

    private void TryReplan()
    {
        if (BuildClimbRoute())
        {
            climbing = true; climbIndex = 0;
            routePlayerAnchor = player.position;
            ResetStuckWatch();
        }
        nextReplanTime = Time.time + replanCooldown;
    }

    private void ResetStuckWatch()
    {
        stuckTimer = 0f;
        lastDistToWP = Mathf.Infinity;
    }

    private void WatchStuck(float currentDist)
    {
        if (currentDist <= lastDistToWP - stuckImproveMin)
        {
            lastDistToWP = currentDist;
            stuckTimer = 0f;
        }
        else
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckWindow && Time.time >= nextReplanTime)
                TryReplan();
        }
    }

    // ===========================
    // ===== TELEPORT MECHANICS ====
    // ===========================
    private void HandleTeleportMechanics()
    {
        if (!player) return;

        // Track player position periodically
        if (Time.time >= nextPlayerTrackTime)
        {
            playerLastKnownPosition = player.position;
            nextPlayerTrackTime = Time.time + playerTrackingRate;
        }

        // Handle teleport preparation
        if (isPreparingTeleport)
        {
            // Stop all movement during preparation
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            
            // Check if preparation time is over
            if (Time.time >= teleportPrepEndTime)
            {
                ExecuteTeleport();
            }
            return;
        }

        // Check if it's time to start teleporting
        if (!isTeleporting && !isPreparingTeleport && Time.time >= nextTeleportTime)
        {
            StartTeleportPreparation();
        }
    }

    private void StartTeleportPreparation()
    {
        isPreparingTeleport = true;
        teleportPrepEndTime = Time.time + teleportPrepTime;
        teleportTargetPosition = playerLastKnownPosition;
        
        // Cancel current climbing if active
        climbing = false;
        
        // Visual/audio feedback could go here
        Debug.Log($"{name} is preparing to teleport to {teleportTargetPosition}!");
    }

    private void ExecuteTeleport()
    {
        // Teleport to the stored position
        transform.position = teleportTargetPosition;
        
        // Reset states
        isPreparingTeleport = false;
        isTeleporting = true;
        
        // Brief post-teleport recovery
        StartCoroutine(TeleportRecovery());
        
        // Set next teleport time
        nextTeleportTime = Time.time + teleportCooldown;
        
        Debug.Log($"{name} teleported to {teleportTargetPosition}!");
    }

    private System.Collections.IEnumerator TeleportRecovery()
    {
        // Brief moment where enemy can't move after teleporting
        yield return new WaitForSeconds(0.1f);
        isTeleporting = false;
    }

    // ======================
    // ===== ATTACK & DBG ===
    // ======================
    public override void Attack()
    {
        // implement anim/damage di sini
        Debug.Log($"{name} attacking!");
    }

    private void DebugDrawRays(Vector2 origin,
                               RaycastHit2D groundInFront,
                               Vector2 gapOrigin,
                               RaycastHit2D gapAhead,
                               RaycastHit2D platformAbove)
    {
        Debug.DrawLine(origin, origin + new Vector2(dir * frontCheckDist, 0f), Color.yellow);
        Debug.DrawLine(gapOrigin, gapOrigin + Vector2.down * gapCheckDownDist, Color.cyan);
        Debug.DrawLine(origin, origin + Vector2.up * overheadCheck, Color.magenta);

        for (int i = 0; i < climbWaypoints.Count; i++)
        {
            var p = climbWaypoints[i];
            Debug.DrawLine(p + Vector2.left * 0.1f,  p + Vector2.right * 0.1f, Color.white);
            Debug.DrawLine(p + Vector2.up   * 0.1f,  p + Vector2.down  * 0.1f, Color.white);
            if (i > 0) Debug.DrawLine(climbWaypoints[i - 1], p, Color.gray);
        }
    }

    private void OnDrawGizmosSelected()
    {
        int gizDir = dir == 0 ? 1 : dir;

        // Ground check
        Gizmos.color = Color.green;
        Vector2 gc = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(gc, groundCheckRadius);

        // depan/jurang/atas
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(gizDir * frontCheckDist, 0f, 0f));

        Gizmos.color = Color.cyan;
        Vector3 go = transform.position + new Vector3(gizDir * frontCheckDist, 0f, 0f);
        Gizmos.DrawLine(go, go + Vector3.down * gapCheckDownDist);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * overheadCheck);

        // Waypoints
        Gizmos.color = Color.white;
        foreach (var wp in climbWaypoints) Gizmos.DrawWireSphere(wp, 0.12f);

        // === GIZMOS: visualisasi grid scan ===
        if (scanDebugLines != null)
        {
            foreach (var line in scanDebugLines)
            {
                Gizmos.color = line.hit ? Color.green : Color.red;
                Gizmos.DrawLine(line.origin, line.end);
                Gizmos.DrawWireSphere(line.origin, 0.05f);
            }
        }

        // === GIZMOS: teleport visualization (Impossible difficulty) ===
        if (difficulty == Difficulty.Impossible)
        {
            if (isPreparingTeleport)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                Gizmos.DrawLine(transform.position, teleportTargetPosition);
                Gizmos.DrawWireSphere(teleportTargetPosition, 0.3f);
            }
            
            // Show player last known position
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(playerLastKnownPosition, Vector3.one * 0.2f);
        }
    }
}
