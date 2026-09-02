using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;

public class UI : MonoBehaviour
{
    public GameObject mainMenuObject;
    public GameObject settingsMenuObject;
    public Toggle doChunkAnimationToggle;
    public Toggle doDaylightCycleToggle;
    public Slider viewDistanceSlider;
    public Slider mouseSensitivitySlider;
    public TMP_Text viewDistanceLabel;
    public TMP_Text mouseSensitivityLabel;
    public TMP_InputField seedInputField;
    Settings settings;

    private void Start()
    {
        mainMenuObject.SetActive(true);
        settingsMenuObject.SetActive(false);

        // Drives the labels while the user drags. GetSettings only covers the
        // initial value, so the sliders have to push updates themselves.
        viewDistanceSlider.onValueChanged.AddListener(value => UpdateViewDistanceLabel(value));
        mouseSensitivitySlider.onValueChanged.AddListener(value => UpdateMouseSensitivityLabel(value));

        GetSettings();
    }

    void UpdateViewDistanceLabel(float value)
    {
        // The labels are optional decoration, so a scene without them still runs.
        if (viewDistanceLabel != null)
        {
            viewDistanceLabel.text = "View Distance: " + value.ToString();
        }
    }

    void UpdateMouseSensitivityLabel(float value)
    {
        if (mouseSensitivityLabel != null)
        {
            mouseSensitivityLabel.text = "Mouse Sensitivity: " + value.ToString("0.0");
        }
    }
    
    public void StartGame()
    {
        SaveSettings();
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
    }

    public void EnterSettings()
    {
        mainMenuObject.SetActive(false);
        settingsMenuObject.SetActive(true);
    }

    public void LeaveSettings()
    {
        SaveSettings();

        mainMenuObject.SetActive(true);
        settingsMenuObject.SetActive(false);
    }

    public void GetSettings()
    {
        // Load what's on disk. Constructing a new Settings here instead would zero
        // every field and the WriteAllText below would persist that over the file.
        string path = Application.dataPath + "/settings.cfg";
        if (File.Exists(path))
        {
            settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
        }

        // Only write when there was nothing (or something unparseable) to read.
        if (settings == null)
        {
            settings = new Settings();
            File.WriteAllText(path, JsonUtility.ToJson(settings));
        }

        doChunkAnimationToggle.isOn = settings.doChunkAnimation;
        doDaylightCycleToggle.isOn = settings.doDaylightCycle;
        viewDistanceSlider.value = settings.viewDistance;
        mouseSensitivitySlider.value = settings.mouseSensitivity;
        seedInputField.text = settings.seed.ToString();

        // Setting .value only fires onValueChanged when the value actually changed,
        // so refresh the labels directly to cover the case where it didn't.
        UpdateViewDistanceLabel(viewDistanceSlider.value);
        UpdateMouseSensitivityLabel(mouseSensitivitySlider.value);
    }

    public void SaveSettings()
    {
        settings.doChunkAnimation = doChunkAnimationToggle.isOn;
        settings.doDaylightCycle = doDaylightCycleToggle.isOn;
        settings.viewDistance = (int)viewDistanceSlider.value;
        settings.mouseSensitivity = mouseSensitivitySlider.value;
        if (string.IsNullOrEmpty(seedInputField.text))
        {
            seedInputField.text = Random.Range(0, int.MaxValue).ToString();
        }
        settings.seed = int.Parse(seedInputField.text);

        string jsonExport = JsonUtility.ToJson(settings);
        File.WriteAllText(Application.dataPath + "/settings.cfg", jsonExport);
    }

    public void QuitGame()
    {
        SaveSettings();
        Application.Quit();
    }
}
