using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 실제 GameScene과 프리팹으로 실행하는 Play Mode 통합 검사.
// Unity -batchmode -projectPath <project> -executeMethod PoolingSmokeCheck.Run -logFile <log>
[InitializeOnLoad]
public static class PoolingSmokeCheck
{
    private const string RunningKey = "PoolingSmokeCheck.Running";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator _checks;
    private static double _deadline;
    private static int _lastFrame;
    private static string _runtimeError;

    static PoolingSmokeCheck()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/01.Scenes/GameScene.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;

        _checks = CheckPooling();
        _deadline = EditorApplication.timeSinceStartup + 90d;
        _lastFrame = -1;
        _runtimeError = null;
        Application.logMessageReceived += OnLogMessage;
        EditorApplication.update += Tick;
    }

    private static void OnLogMessage(string message, string stackTrace, LogType type)
    {
        // 새 복사본의 검색 인덱스 초기화 오류는 게임 런타임 검사와 무관하다.
        if (stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _runtimeError = message;
        }
    }

    private static void Tick()
    {
        try
        {
            if (_runtimeError != null) throw new Exception(_runtimeError);
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("Pooling checks timed out.");
            if (Time.frameCount == _lastFrame) return;

            _lastFrame = Time.frameCount;
            if (!_checks.MoveNext()) Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("POOLING CHECK FAILED: " + exception);
            Finish(1);
        }
    }

    private static void Finish(int exitCode)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLogMessage;
        if (exitCode == 0) Debug.Log("POOLING CHECK PASSED: all integration checks completed.");
        EditorApplication.Exit(exitCode);
    }

    private static IEnumerator CheckPooling()
    {
        PlayerGunFire gun = Object.FindFirstObjectByType<PlayerGunFire>();
        PlayerBombFire thrower = Object.FindFirstObjectByType<PlayerBombFire>();
        Require(gun != null && thrower != null, "Scene weapon components must exist.");

        // 입력과 카메라 추적을 멈추고 실제 무기 처리를 같은 씬에서 호출한다.
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            behaviour.enabled = false;
        }

        Time.timeScale = 5f;
        Camera.main.transform.rotation = Quaternion.identity;
        GameObject muzzle = new GameObject("Pooling Test Muzzle");
        muzzle.transform.position = new Vector3(10000f, 100f, 10000f);
        Set(gun, "_firePoint", muzzle.transform);
        Set(thrower, "_firePoint", muzzle.transform);

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = muzzle.transform.position + Vector3.forward * 5f;
        wall.transform.localScale = new Vector3(10f, 10f, 1f);
        Physics.SyncTransforms();

        ParticleEffectPool hits = Get<ParticleEffectPool>(gun, "_hitEffectPool");
        Invoke(gun, "Shoot");
        Require(Get<int>(gun, "_currentAmmo") == 49, "Raycast shot must consume one round.");
        PooledParticleEffect firstHit = hits.GetComponentInChildren<PooledParticleEffect>();
        Require(firstHit != null, "Raycast must produce a pooled hit effect.");
        int hitId = firstHit.GetInstanceID();
        Require(Vector3.Distance(firstHit.transform.position, muzzle.transform.position + Vector3.forward * 4.5f) < 0.05f,
            "Hit effect must appear at the raycast contact point.");
        Require(Vector3.Dot(firstHit.transform.forward, Vector3.back) > 0.99f,
            "Hit effect must face the surface normal.");
        foreach (ParticleSystem particles in firstHit.GetComponentsInChildren<ParticleSystem>())
        {
            Require(particles.main.stopAction == ParticleSystemStopAction.None, "Pooled particles must not destroy themselves.");
        }
        while (firstHit.gameObject.activeSelf) yield return null;

        Invoke(gun, "Shoot");
        Require(hits.GetComponentInChildren<PooledParticleEffect>().GetInstanceID() == hitId,
            "A later hit must reuse the same instance.");
        for (int i = 0; i < 5; i++) Invoke(gun, "Shoot");
        Require(hits.GetComponentsInChildren<PooledParticleEffect>().Length == 6,
            "Overlapping hits must use independent active instances.");
        while (hits.GetComponentInChildren<PooledParticleEffect>() != null) yield return null;
        Require(hits.GetComponentsInChildren<PooledParticleEffect>(true).Length == 6,
            "Finished hit effects must remain available for reuse.");
        Debug.Log("POOLING CHECK: raycast placement, normal, ammo, reuse and overlapping effects passed.");

        float reloadStartedAt = Time.time;
        int ammoBeforeReload = Get<int>(gun, "_currentAmmo");
        Require(Mathf.Approximately(Get<float>(gun, "_reloadDuration"), 1.5f), "Scene reload duration must remain 1.5 seconds.");
        Invoke(gun, "Reload");
        Require(Get<bool>(gun, "_isReloading"), "Reload must immediately block shooting.");
        Coroutine reload = Get<Coroutine>(gun, "_reloadCoroutine");
        Invoke(gun, "Reload");
        Require(Get<Coroutine>(gun, "_reloadCoroutine") == reload, "Repeated reload must not start another coroutine.");
        Invoke(gun, "Shoot");
        Require(Get<int>(gun, "_currentAmmo") == ammoBeforeReload, "Shooting during reload must not consume ammo.");
        while (Get<bool>(gun, "_isReloading"))
        {
            Require(Get<int>(gun, "_currentAmmo") == ammoBeforeReload, "Ammo must remain unchanged until reload completes.");
            yield return null;
        }
        Require(Time.time - reloadStartedAt >= 1.5f, "Reload must wait at least 1.5 game seconds.");
        Require(Get<int>(gun, "_currentAmmo") == 50, "Reload must refill the magazine.");
        Require(Get<UnityEngine.UI.Text>(gun, "_ammoText").text == "50/50", "Reload must refresh ammo UI.");
        Invoke(gun, "Reload");
        Require(Get<Coroutine>(gun, "_reloadCoroutine") == null, "A full magazine must not start reloading.");

        Set(gun, "_currentAmmo", 49);
        gun.enabled = true;
        Invoke(gun, "Reload");
        gun.enabled = false;
        Require(!Get<bool>(gun, "_isReloading") && Get<Coroutine>(gun, "_reloadCoroutine") == null,
            "Disabling the weapon must cancel reload and clear its state.");
        float cancellationStartedAt = Time.time;
        while (Time.time - cancellationStartedAt < 1.6f) yield return null;
        Require(Get<int>(gun, "_currentAmmo") == 49, "Cancelled reload must not refill later.");
        Invoke(gun, "Reload");
        while (Get<bool>(gun, "_isReloading")) yield return null;
        Require(Get<int>(gun, "_currentAmmo") == 50, "Reload must work again after cancellation.");
        Debug.Log("POOLING CHECK: 1.5-second coroutine reload, shooting guard, duplicate guard, UI and cancellation passed.");

        Set(gun, "_currentAmmo", 10);
        Set(gun, "_fireTimer", 0f);
        Invoke(gun, "Reload");
        Invoke(gun, "TryShoot");
        Require(!Get<bool>(gun, "_isReloading") && Get<Coroutine>(gun, "_reloadCoroutine") == null,
            "Fire input must stop the running reload coroutine.");
        Require(Get<int>(gun, "_currentAmmo") == 9, "Cancelling reload by firing must consume one remaining round.");
        Require(Get<UnityEngine.UI.Text>(gun, "_ammoText").text == "9/50", "Cancel-and-fire must update ammo UI.");
        float interruptedAt = Time.time;
        while (Time.time - interruptedAt < 1.6f) yield return null;
        Require(Get<int>(gun, "_currentAmmo") == 9, "Interrupted reload must not refill the magazine later.");

        Set(gun, "_fireTimer", 0.05f);
        Invoke(gun, "Reload");
        Invoke(gun, "TryShoot");
        Require(!Get<bool>(gun, "_isReloading") && Get<int>(gun, "_currentAmmo") == 9,
            "Fire input must cancel reload immediately while respecting the fire interval.");

        Set(gun, "_currentAmmo", 0);
        Set(gun, "_fireTimer", 0f);
        Invoke(gun, "Reload");
        Invoke(gun, "TryShoot");
        Require(!Get<bool>(gun, "_isReloading") && Get<int>(gun, "_currentAmmo") == 0,
            "Fire input with empty ammo must cancel reload without firing.");
        Invoke(gun, "Reload");
        while (Get<bool>(gun, "_isReloading")) yield return null;
        Require(Get<int>(gun, "_currentAmmo") == 50, "Reload must still complete after a fire cancellation.");
        Debug.Log("POOLING CHECK: fire cancels reload, fires remaining ammo, preserves fire interval and handles empty ammo.");

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = muzzle.transform.position + Vector3.down * 3f;
        floor.transform.localScale = new Vector3(20f, 1f, 20f);
        Physics.SyncTransforms();
        Camera.main.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        Set(thrower, "_throwPower", 7f);
        Invoke(thrower, "Throw");
        Require(Get<int>(thrower, "_currentAmmo") == 9, "Throw must consume one grenade.");
        Require(Mathf.Approximately(Get<float>(thrower, "_throwPower"), Get<float>(thrower, "_minThrowPower")),
            "Throw must reset charge power.");
        BombPool bombs = Get<BombPool>(thrower, "_bombPool");
        Bomb firstBomb = bombs.GetComponentInChildren<Bomb>();
        int bombId = firstBomb.GetInstanceID();
        while (firstBomb.gameObject.activeSelf) yield return null;
        ParticleEffectPool explosions = bombs.GetComponentInChildren<ParticleEffectPool>();
        PooledParticleEffect explosion = explosions.GetComponentInChildren<PooledParticleEffect>();
        Require(explosion != null, "Collision must play an explosion after the grenade returns.");
        int explosionId = explosion.GetInstanceID();
        Rigidbody body = firstBomb.GetComponent<Rigidbody>();
        Require(body.linearVelocity == Vector3.zero && body.angularVelocity == Vector3.zero,
            "Returned grenade must have no residual motion.");
        while (explosion.gameObject.activeSelf) yield return null;

        Invoke(thrower, "Throw");
        Require(bombs.GetComponentInChildren<Bomb>().GetInstanceID() == bombId, "Next throw must reuse the grenade.");
        while (firstBomb.gameObject.activeSelf) yield return null;
        Require(explosions.GetComponentInChildren<PooledParticleEffect>().GetInstanceID() == explosionId,
            "Next explosion must reuse its effect.");
        while (explosion.gameObject.activeSelf) yield return null;
        Debug.Log("POOLING CHECK: grenade collision, charge reset, ammo, motion reset and explosion reuse passed.");

        Set(firstBomb, "_maxLifetime", 0.2f);
        body.angularVelocity = Vector3.one * 20f;
        bombs.Throw(muzzle.transform.position + Vector3.up * 100f, Vector3.up, 5f);
        Require(body.angularVelocity == Vector3.zero, "Relaunch must clear previous angular velocity.");
        while (firstBomb.gameObject.activeSelf) yield return null;
        Require(explosions.GetComponentInChildren<PooledParticleEffect>() == null,
            "A grenade that never collides must be reclaimed without an explosion.");

        Set(thrower, "_currentAmmo", 0);
        Set(thrower, "_throwPower", 12f);
        Invoke(thrower, "Throw");
        Require(bombs.GetComponentInChildren<Bomb>() == null, "Empty ammo must not launch a grenade.");
        Require(Mathf.Approximately(Get<float>(thrower, "_throwPower"), Get<float>(thrower, "_minThrowPower")),
            "Empty throw must reset charge power.");

        // 보관 한도보다 많은 동시 피격도 재생 후에는 50개까지만 남는다.
        for (int i = 0; i < 55; i++) hits.Play(muzzle.transform.position, Quaternion.identity);
        while (hits.GetComponentInChildren<PooledParticleEffect>() != null) yield return null;
        yield return null;
        Require(hits.GetComponentsInChildren<PooledParticleEffect>(true).Length == 50,
            "Returning more effects than the storage limit must destroy only the excess.");

        hits.Play(muzzle.transform.position, Quaternion.identity);
        bombs.Throw(muzzle.transform.position, Vector3.up, 5f);
        Object.Destroy(gun.gameObject);
        yield return null;
        yield return null;
        Require(hits == null && bombs == null && explosions == null,
            "Destroying the owner must remove its pools and active instances.");
        Debug.Log("POOLING CHECK: missed-grenade timeout, empty ammo, pool capacity and owner cleanup passed.");

    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static T Get<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    }
}
