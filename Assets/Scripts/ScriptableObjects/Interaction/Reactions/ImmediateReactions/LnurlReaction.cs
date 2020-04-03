using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LnurlReaction : Reaction
{
    private SceneController sceneController;    // Reference to the SceneController to actually do the loading and unloading of scenes.


    protected override void SpecificInit()
    {
        sceneController = FindObjectOfType<SceneController>();

    }


    protected override void ImmediateReaction()
    {
        sceneController.DoWithdrawal();
    }
}
