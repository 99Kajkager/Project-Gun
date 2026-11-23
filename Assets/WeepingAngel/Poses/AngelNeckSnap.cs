using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;

/// <summary>
/// When the angel catches the player: lock the player, snap the camera 90 degrees sideways
/// (broken neck), play an FMOD sound, then fade to a simple GAME OVER screen with a Restart button.
/// Add this to the same object as WeepingAngel. It hooks WeepingAngel.onCaught automatically.
/// Requires FMOD for Unity 2.0+ (same as AngelFootsteps).
/// </summary>
[RequireComponent(typeof(WeepingAngel))]
public class AngelNeckSnap : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("The neck-snap FMOD event.")]
    public EventReference snapEvent;

    [Header("Neck snap")]
    [Tooltip("How far the head is bent sideways.")]
    public float snapAngle = 90f;
    [Tooltip("How fast the snap happens (seconds). Keep it tiny.")]
    public float snapDuration = 0.06f;
    [Tooltip("Small extra downward tilt so it looks limp rather than just rolled.")]
    public float extraPitch = 15f;

    [Header("Player lock")]
    [Tooltip("Movement / mouse-look scripts to disable on catch. Leave empty to auto-disable every script " +
             "on the player and on the camera's parents (FMOD components are skipped).")]
    public Behaviour[] disableOnCatch;

    [Header("Game over screen")]
    public string title = "GAME OVER";
    public float gameOverDelay = 1.5f;
    public float fadeDuration = 1.0f;

    WeepingAngel angel;
    bool triggered, showScreen;
    float fade;
    GUIStyle titleStyle, buttonStyle;

    void Awake()
    {
        angel = GetComponent<WeepingAngel>();
        angel.onCaught.AddListener(OnCaught);
    }

    void OnDestroy()
    {
        if (angel != null) angel.onCaught.RemoveListener(OnCaught);
    }

    void OnCaught()
    {
        if (triggered) return;
        triggered = true;
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        Camera cam = angel.playerCamera != null ? angel.playerCamera : Camera.main;

        LockPlayer(cam);

        if (!snapEvent.IsNull)
            RuntimeManager.PlayOneShot(snapEvent, cam != null ? cam.transform.position : transform.position);

        // Snap the neck: roll the camera toward the side the angel is standing on.
        if (cam != null)
        {
            Transform ct = cam.transform;
            float side = Vector3.Dot(ct.right, transform.position - ct.position) >= 0f ? 1f : -1f;
            Quaternion from = ct.localRotation;
            Quaternion to = from * Quaternion.Euler(extraPitch, 0f, -side * snapAngle);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.001f, snapDuration);
                ct.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
            ct.localRotation = to;
        }

        yield return new WaitForSeconds(gameOverDelay);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        showScreen = true;

        while (fade < 1f)
        {
            fade += Time.deltaTime / Mathf.Max(0.01f, fadeDuration);
            yield return null;
        }
        fade = 1f;
    }

    void LockPlayer(Camera cam)
    {
        if (disableOnCatch != null && disableOnCatch.Length > 0)
        {
            foreach (var b in disableOnCatch) if (b != null) b.enabled = false;
            return;
        }

        // Auto mode: every MonoBehaviour on the player and on the camera's parent chain.
        var list = new System.Collections.Generic.List<MonoBehaviour>();
        if (angel.player != null) list.AddRange(angel.player.GetComponentsInChildren<MonoBehaviour>(false));
        if (cam != null) list.AddRange(cam.GetComponentsInParent<MonoBehaviour>(false));

        foreach (var mb in list)
        {
            if (mb == null || mb == this || mb == angel) continue;
            string ns = mb.GetType().Namespace;
            if (ns != null && ns.StartsWith("FMOD")) continue; // keep audio listener alive
            mb.enabled = false;
        }
    }

    void OnGUI()
    {
        if (!showScreen) return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }
        titleStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.12f);
        buttonStyle.fontSize = Mathf.RoundToInt(Screen.height * 0.04f);

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, fade);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = new Color(0.75f, 0.05f, 0.05f, fade);
        GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, Screen.height * 0.25f), title, titleStyle);

        GUI.color = new Color(1f, 1f, 1f, fade);
        float w = Screen.width * 0.2f, h = Screen.height * 0.08f;
        if (fade >= 1f && GUI.Button(new Rect((Screen.width - w) * 0.5f, Screen.height * 0.6f, w, h), "Restart", buttonStyle))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        GUI.color = prev;
    }
}
