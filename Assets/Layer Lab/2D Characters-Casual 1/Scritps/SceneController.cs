using System;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LayerLab.Casual2DCharacter
{
   public class SceneController : MonoBehaviour
   {
      private int skinIndex;
      private int characterIndex;
      [SerializeField] private Sprite[] spriteButtonBgs;
      [SerializeField] private Image[] imageButtonBgs;
      [SerializeField] private Transform parentCharacter;
      [SerializeField] private TMP_Text textCharacterName;

      //Spine
      private SkeletonGraphic[] skeletonGraphics;
      [SerializeField] private SkeletonDataAsset[] skeletonDataAssets;

      private void Start()
      {
         skeletonGraphics = parentCharacter.GetComponentsInChildren<SkeletonGraphic>();
         UIUpdate();
         UpdateCharacter();
      }

      #region ButtonEvent
      public void OnClick_Tap(int tapIndex)
      {
         skinIndex = tapIndex;
         UIUpdate();
         UpdateCharacter();
      }
      
      public void OnClick_Previous()
      {
         characterIndex--;
         if (characterIndex < 0) characterIndex = skeletonDataAssets.Length - 1;
         UpdateCharacter();
      }

      public void OnClick_Next()
      {
         characterIndex++;
         if (characterIndex > skeletonDataAssets.Length - 1) characterIndex = 0;
         UpdateCharacter();
      }

      void Update()
      {
#if ENABLE_INPUT_SYSTEM
         if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
         {
            OnClick_Previous();
         }
         else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
         {
            OnClick_Next();
         }
#else
         if (Input.GetKeyDown(KeyCode.LeftArrow))
         {
            OnClick_Previous();
         }
         else if (Input.GetKeyDown(KeyCode.RightArrow))
         {
            OnClick_Next();
         }
#endif
      }
      #endregion

      
      
      

      private void UpdateCharacter()
      {
         UpdateDataAssets();
         UpdateSkin();
         UpdateName();
      }


      private void UpdateName()
      {
         textCharacterName.text = skeletonDataAssets[characterIndex].name.Split("_")[0];
      }
      
      private void UpdateDataAssets()
      {
         //캐릭터 변경
         for (int i = 0; i < skeletonGraphics.Length; i++)
         {
            skeletonGraphics[i].skeletonDataAsset = skeletonDataAssets[characterIndex];
            skeletonGraphics[i].Initialize(true);
         }
      }

      private void UpdateSkin()
      {
         for (int i = 0; i < skeletonGraphics.Length; i++) ChangeSpineSkinGraphic(skeletonGraphics[i], $"S0{skinIndex}");
      }

      private void UIUpdate()
      {
         if (skinIndex < 0) skinIndex = imageButtonBgs.Length - 1;
         if (skinIndex > imageButtonBgs.Length) skinIndex = 0;

         for (int i = 0; i < imageButtonBgs.Length; i++)
         {
            if (i == skinIndex)
            {
               imageButtonBgs[i].sprite = spriteButtonBgs[1];
            }
            else
            {
               imageButtonBgs[i].sprite = spriteButtonBgs[0];
            }
         }
      }

      public static void ChangeSpineSkinGraphic(SkeletonGraphic anim, string skinName)
      {
         if (anim.Skeleton == null)
            return;

         anim.Skeleton.SetSkin(skinName);
         anim.Skeleton.SetSlotsToSetupPose();
         anim.LateUpdate();
      }
   }
}

