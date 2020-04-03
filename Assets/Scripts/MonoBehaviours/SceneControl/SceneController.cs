using System;
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZbdUnitySDK.Logging;
using ZbdUnitySDK;
using System.Threading;
using System.Threading.Tasks;
using ZbdUnitySDK.Models;
using ZbdUnitySDK.Models.Zebedee;
using ZbdUnitySDK.Services;
using ZXing;
using ZXing.QrCode;

// This script exists in the Persistent scene and manages the content
// based scene's loading.  It works on a principle that the
// Persistent scene will be loaded first, then it loads the scenes that
// contain the player and other visual elements when they are needed.
// At the same time it will unload the scenes that are not needed when
// the player leaves them.
public class SceneController : MonoBehaviour
{
    public event Action BeforeSceneUnload;          // Event delegate that is called just before a scene is unloaded.
    public event Action AfterSceneLoad;             // Event delegate that is called just after a scene is loaded.


    public CanvasGroup faderCanvasGroup;            // The CanvasGroup that controls the Image used for fading to black.
    public float fadeDuration = 1f;                 // How long it should take to fade to and from black.
    public string startingSceneName = "SecurityRoom";
    // The name of the scene that should be loaded first.
    public string initialStartingPositionName = "DoorToMarket";
    // The name of the StartingPosition in the first scene to be loaded.
    public SaveData playerSaveData;                 // Reference to the ScriptableObject which stores the name of the StartingPosition in the next scene.


    private bool isFading;                          // Flag used to determine if the Image is currently fading to or from black.


    public string apiKey;
    public string zebedeeBaseUrl;
    public Image QRcodeImage;
    public GameObject QRCodePanel;
    public Text QRcodeText;

    private IZdbLogger logger = LoggerFactory.GetLogger();
    private ZebedeeClient zbdClient = null;
    private int totalSats = 10;
    public int gamePlayFeeSats;

    private async void Start()
    {
        // Set the initial alpha to start off with a black screen.
        faderCanvasGroup.alpha = 1f;

        // Write the initial starting position to the playerSaveData so it can be loaded by the player when the first scene is loaded.
        playerSaveData.Save(PlayerMovement.startingPositionKey, initialStartingPositionName);


        QRcodeText.text = "Play Game for " + gamePlayFeeSats + " sats";

        zbdClient = new ZebedeeClient(zebedeeBaseUrl, apiKey);

        PayForAGamePlay();

    }

    ///////////////// START USING  ZEBEDEE SDK  /////////////////////////////////////////////////
    private async void PayForAGamePlay()
    {



        Charge charge = new Charge();
        charge.Description = gamePlayFeeSats + " sats for ZEBEDEE SDK DEMO GAME";
        charge.AmountInSatoshi = int.Parse(gamePlayFeeSats + "");
        Debug.Log(zebedeeBaseUrl + " " + apiKey + " " + gamePlayFeeSats);

        await zbdClient.CreateChargeAsync(charge, handleInvoice);



    }



    private async void handleInvoice(ChargeResponse invoice)
    {
        //3.Lightning BOLT invoice string
        string boltInvoice = invoice.Data.Invoice.Request;
        string chargeId = invoice.Data.Id;
        if (string.IsNullOrEmpty(boltInvoice))
        {
            Debug.Log("bolt Invoice is not set in Invoice in reponse.Check the BTCpay server's lightning setup");
            return;
        }

        Texture2D texs = GenerateQR(boltInvoice);//Generate QR code image

        //4.Set the QR code Image to image Gameobject
        QRcodeImage.GetComponent<Image>().sprite = Sprite.Create(texs, new Rect(0.0f, 0.0f, texs.width, texs.height), new Vector2(0.5f, 0.5f), 100.0f);

        //5.Subscribe the get notified about payment status
        string status = await zbdClient.SubscribeChargeAsync(chargeId);

        if ("completed".Equals(status))
        {
            //Change the image from QR to Paid
            QRcodeImage.GetComponent<Image>().sprite = Resources.Load<Sprite>("image/paid");
            logger.Debug("payment is complete");
            await Task.Delay(2000);
            QRCodePanel.SetActive(false);
            totalSats += gamePlayFeeSats;

            StartCoroutine(LoadSceneAndSetActive(startingSceneName));

            // Once the scene is finished loading, start fading in.
            StartCoroutine(Fade(0f));


        }
        else
        {
            //for example, if the amount paid is not full, do something.the line below just print the status.
            logger.Error("payment is not completed:" + status);
        }
    }


    public async void DoWithdrawal()
    {
        QRCodePanel.SetActive(true);

        //1.New Withdrawal Preparation
        Withdraw withdraw = new Withdraw();
        withdraw.Description = "ZEBEDEE SDK DEMO GAME";
        withdraw.AmountInSatoshi = totalSats;

        //2.Create Invoice with initial data and get the full invoice
        await zbdClient.WithDrawAsync(withdraw, handleWithdrawal);
    }

    private async void handleWithdrawal(WithdrawResponse withdraw)
    {
        string lnURL = withdraw.Data.Invoice.Request;
        if (string.IsNullOrEmpty(lnURL))
        {
            logger.Debug("lnURL is not set in withdrawal response.");
            logger.Debug(withdraw.Data.Invoice.Request);
            return;
        }
        QRcodeText.text = "Congrats! Withdraw " + gamePlayFeeSats + " sats";

        Texture2D texs = GenerateQR(lnURL);//Generate QR code image

        //4.Set the QR code image to image Gameobject
        QRcodeImage.GetComponent<Image>().sprite = Sprite.Create(texs, new Rect(0.0f, 0.0f, texs.width, texs.height), new Vector2(0.5f, 0.5f), 100.0f);

        //5.Subscribe to a callback method with ID to be monitored
        string status = await zbdClient.SubscribeWithDrawAsync(withdraw.Data.Id);

        if ("completed".Equals(status))
        {
            //Change the image from QR to Paid
            QRcodeImage.GetComponent<Image>().sprite = Resources.Load<Sprite>("image/withdrawn");
            logger.Debug("withdraw is success");
        }
        else
        {
            //for example, if the amount paid is not full, do something.the line below just print the status.
            logger.Error("withdraw is not success:" + status);
        }


    }

    private Texture2D GenerateQR(string text)
    {
        logger.Debug("generateQR():generateing Qr for text: " + text);

        var encoded = new Texture2D(350, 350);
        var color32 = Encode(text, encoded.width, encoded.height);
        encoded.SetPixels32(color32);
        encoded.Apply();
        return encoded;
    }

    private static Color32[] Encode(string textForEncoding,
      int width, int height)
    {

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Height = height,
                Width = width
            }
        };

        return writer.Write(textForEncoding);
    }

    ///////////////// END USING  ZEBEDEE SDK  ////////////////////////////////////////////////

    // This is the main external point of contact and influence from the rest of the project.
    // This will be called by a SceneReaction when the player wants to switch scenes.
    public void FadeAndLoadScene(SceneReaction sceneReaction)
    {
        // If a fade isn't happening then start fading and switching scenes.
        if (!isFading)
        {
            StartCoroutine(FadeAndSwitchScenes(sceneReaction.sceneName));
        }
    }


    // This is the coroutine where the 'building blocks' of the script are put together.
    private IEnumerator FadeAndSwitchScenes(string sceneName)
    {
        // Start fading to black and wait for it to finish before continuing.
        yield return StartCoroutine(Fade(1f));

        // If this event has any subscribers, call it.
        if (BeforeSceneUnload != null)
            BeforeSceneUnload();

        // Unload the current active scene.
        yield return SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().buildIndex);

        // Start loading the given scene and wait for it to finish.
        yield return StartCoroutine(LoadSceneAndSetActive(sceneName));

        // If this event has any subscribers, call it.
        if (AfterSceneLoad != null)
            AfterSceneLoad();

        // Start fading back in and wait for it to finish before exiting the function.
        yield return StartCoroutine(Fade(0f));
    }


    private IEnumerator LoadSceneAndSetActive(string sceneName)
    {
        // Allow the given scene to load over several frames and add it to the already loaded scenes (just the Persistent scene at this point).
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        // Find the scene that was most recently loaded (the one at the last index of the loaded scenes).
        Scene newlyLoadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

        // Set the newly loaded scene as the active scene (this marks it as the one to be unloaded next).
        SceneManager.SetActiveScene(newlyLoadedScene);
    }


    private IEnumerator Fade(float finalAlpha)
    {
        // Set the fading flag to true so the FadeAndSwitchScenes coroutine won't be called again.
        isFading = true;

        // Make sure the CanvasGroup blocks raycasts into the scene so no more input can be accepted.
        faderCanvasGroup.blocksRaycasts = true;

        // Calculate how fast the CanvasGroup should fade based on it's current alpha, it's final alpha and how long it has to change between the two.
        float fadeSpeed = Mathf.Abs(faderCanvasGroup.alpha - finalAlpha) / fadeDuration;

        // While the CanvasGroup hasn't reached the final alpha yet...
        while (!Mathf.Approximately(faderCanvasGroup.alpha, finalAlpha))
        {
            // ... move the alpha towards it's target alpha.
            faderCanvasGroup.alpha = Mathf.MoveTowards(faderCanvasGroup.alpha, finalAlpha,
                fadeSpeed * Time.deltaTime);

            // Wait for a frame then continue.
            yield return null;
        }

        // Set the flag to false since the fade has finished.
        isFading = false;

        // Stop the CanvasGroup from blocking raycasts so input is no longer ignored.
        faderCanvasGroup.blocksRaycasts = false;
    }
}
