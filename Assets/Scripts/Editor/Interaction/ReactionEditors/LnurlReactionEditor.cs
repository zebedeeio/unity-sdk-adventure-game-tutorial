using UnityEditor;

[CustomEditor(typeof(LnurlReaction))]
public class LnurlReactionEditor : ReactionEditor
{
    protected override string GetFoldoutLabel()
    {
        return "LNURL Reaction";
    }
}
