﻿using DG.Tweening;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI;
using Kingmaker.UI.Constructor;
using ModMaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using TurnBased.Controllers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static TurnBased.Main;
using static TurnBased.Utility.SettingsWrapper;
using static TurnBased.Utility.StatusWrapper;

namespace TurnBased.UI
{
    public class CombatTrackerManager : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private RectTransform _body;
        private VerticalLayoutGroup _bodyLayoutGroup;
        private ButtonWrapper _buttonEndTurn;
        private ButtonWrapper _buttonFiveFoorStep;
        private ButtonWrapper _buttonDelay;
        private RectTransform _unitButtons;
        private UnitButtonManager _unitButtonTemplate;

        private bool _enabled;
        private float _scale;
        private float _width;

        private Dictionary<UnitEntityData, UnitButtonManager> _unitButtonDic = new Dictionary<UnitEntityData, UnitButtonManager>();

        public UnitEntityData HoveringUnit { get; private set; }

        public static CombatTrackerManager CreateObject()
        {
            UICommon uiCommon = Game.Instance.UI.Common;
            GameObject hudLayout = uiCommon?.transform.Find("HUDLayout")?.gameObject;
            GameObject escMenuButtonBlock = uiCommon?.transform.Find("EscMenuWindow/Window/ButtonBlock")?.gameObject;

            if (!hudLayout || !escMenuButtonBlock)
                return null;

            // initialize window
            GameObject combatTracker = new GameObject("TurnBasedCombatTracker", typeof(RectTransform), typeof(CanvasGroup));
            combatTracker.transform.SetParent(hudLayout.transform);
            combatTracker.transform.SetSiblingIndex(0);

            RectTransform rectCombatTracker = (RectTransform)combatTracker.transform;
            rectCombatTracker.anchorMin = new Vector2(0f, 0f);
            rectCombatTracker.anchorMax = new Vector2(1f, 1f);
            rectCombatTracker.pivot = new Vector2(1f, 1f);
            rectCombatTracker.position = Camera.current.ScreenToWorldPoint(new Vector3(
                Screen.width, Screen.height, Camera.current.WorldToScreenPoint(hudLayout.transform.position).z));
            rectCombatTracker.position = rectCombatTracker.position - rectCombatTracker.forward;
            rectCombatTracker.rotation = Quaternion.identity;

            // initialize button block
            GameObject body = Instantiate(escMenuButtonBlock, combatTracker.transform, false);
            body.name = "Body";

            Image imgBody = body.GetComponent<Image>();
            Image imgMetamagic = uiCommon.transform.Find("ServiceWindow/SpellBook/ContainerNoBook/Background")?.gameObject.GetComponent<Image>();
            if (imgMetamagic)
                imgBody.sprite = imgMetamagic.sprite;

            RectTransform rectBody = (RectTransform)body.transform;
            rectBody.anchorMin = new Vector2(1f, 1f);
            rectBody.anchorMax = new Vector2(1f, 1f);
            rectBody.pivot = new Vector2(1f, 1f);
            rectBody.localPosition = new Vector3(0f, 0f, 0f);
            rectBody.rotation = Quaternion.identity;

            ContentSizeFitter contentSizeFitter = body.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.MinSize;

            VerticalLayoutGroup verticalLayoutGroup = body.GetComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childAlignment = TextAnchor.UpperRight;
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childControlHeight = false;
            verticalLayoutGroup.childForceExpandWidth = true;
            verticalLayoutGroup.childForceExpandHeight = false;

            // initialize end turn button
            ButtonPF buttonEndTurn = body.transform.Find("Btn_Quit").gameObject.GetComponent<ButtonPF>();
            buttonEndTurn.name = "Button_EndTurn";

            RectTransform rectButtonEndTurn = (RectTransform)buttonEndTurn.transform;
            rectButtonEndTurn.pivot = new Vector2(1f, 1f);
            rectButtonEndTurn.sizeDelta = new Vector2(0f, UNIT_BUTTON_HEIGHT);
            rectButtonEndTurn.SetSiblingIndex(0);

            // initialize special action buttons
            GameObject actionButtons = new GameObject("SpecialActionButtons", typeof(RectTransform));

            RectTransform rectActionButtons = (RectTransform)actionButtons.transform;
            rectActionButtons.SetParent(rectBody, false);
            rectActionButtons.pivot = new Vector2(1f, 1f);
            rectActionButtons.sizeDelta = new Vector2(0f, UNIT_BUTTON_HEIGHT);
            rectActionButtons.SetSiblingIndex(1);

            // initialize 5-foot step button
            ButtonPF buttonFFS = body.transform.Find("Btn_Save").gameObject.GetComponent<ButtonPF>();
            buttonFFS.name = "Button_FiveFootStep";
            buttonFFS.transform.SetParent(rectActionButtons, false);

            RectTransform rectButtonFFS = (RectTransform)buttonFFS.transform;
            rectButtonFFS.anchorMin = new Vector2(0f, 0f);
            rectButtonFFS.anchorMax = new Vector2(0.5f, 1f);
            rectButtonFFS.pivot = new Vector2(1f, 1f);
            rectButtonFFS.localPosition = new Vector3(0f, 0f, 0f);
            rectButtonFFS.rotation = Quaternion.identity;
            rectButtonFFS.sizeDelta = new Vector2(0f, 0f);

            // initialize delay turn button
            ButtonPF buttonDelay = body.transform.Find("Btn_Load").gameObject.GetComponent<ButtonPF>();
            buttonDelay.name = "Button_Delay";
            buttonDelay.transform.SetParent(rectActionButtons, false);

            RectTransform rectButtonDelay = (RectTransform)buttonDelay.transform;
            rectButtonDelay.anchorMin = new Vector2(0.5f, 0f);
            rectButtonDelay.anchorMax = new Vector2(1f, 1f);
            rectButtonDelay.pivot = new Vector2(1f, 1f);
            rectButtonDelay.localPosition = new Vector3(0f, 0f, 0f);
            rectButtonDelay.rotation = Quaternion.identity;
            rectButtonDelay.sizeDelta = new Vector2(0f, 0f);

            // initialize separator
            RectTransform rectSeparator = body.transform.Find("Separator") as RectTransform;
            rectSeparator.pivot = new Vector2(1f, 1f);
            rectSeparator.sizeDelta = new Vector2(0f, UNIT_BUTTON_SPACE);

            // clear unused buttons
            for (int i = 0; i < body.transform.childCount; i++)
            {
                GameObject child = body.transform.GetChild(i).gameObject;
                if (child.name != "Button_EndTurn" && child.name != "SpecialActionButtons" && child.name != "Separator")
                {
                    child.SafeDestroy();
                    i--;
                }
            }

            // initialize button block (unit buttons)
            GameObject unitButtons = new GameObject("UnitButtons", typeof(RectTransform));

            RectTransform rectUnitButtons = (RectTransform)unitButtons.transform;
            rectUnitButtons.SetParent(rectBody, false);
            rectUnitButtons.pivot = new Vector2(1f, 1f);

            return combatTracker.AddComponent<CombatTrackerManager>();
        }

        void Awake()
        {
            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            _body = (RectTransform)transform.Find("Body");
            _bodyLayoutGroup = _body.GetComponent<VerticalLayoutGroup>();

            _buttonEndTurn = new ButtonWrapper(
                _body.Find("Button_EndTurn").gameObject.GetComponent<ButtonPF>(),
                "End Turn", HandleClickEndTurn);

            _buttonFiveFoorStep = new ButtonWrapper(
                _body.Find("SpecialActionButtons/Button_FiveFootStep").gameObject.GetComponent<ButtonPF>(),
                "5-F. STEP", HandleClickFiveFootStep);

            _buttonDelay = new ButtonWrapper(
                _body.Find("SpecialActionButtons/Button_Delay").gameObject.GetComponent<ButtonPF>(),
                "Delay", HandleClickDelay);

            _unitButtons = (RectTransform)_body.Find("UnitButtons");
        }

        void OnEnable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            HotkeyHelper.Bind(HOTKEY_FOR_FIVE_FOOT_STEP, HandleClickFiveFootStep);
            HotkeyHelper.Bind(HOTKEY_FOR_DELAY, HandleClickDelay);
            HotkeyHelper.Bind(HOTKEY_FOR_END_TURN, HandleClickEndTurn);
        }

        void OnDisable()
        {
            Mod.Debug(MethodBase.GetCurrentMethod());

            HotkeyHelper.Unbind(HOTKEY_FOR_FIVE_FOOT_STEP, HandleClickFiveFootStep);
            HotkeyHelper.Unbind(HOTKEY_FOR_DELAY, HandleClickDelay);
            HotkeyHelper.Unbind(HOTKEY_FOR_END_TURN, HandleClickEndTurn);

            ClearUnits();
            HoveringUnit = null;

            _canvasGroup.DOKill();
            _body.DOKill();
        }

        void OnDestroy()
        {
            _unitButtonTemplate.SafeDestroy();
        }

        void Update()
        {
            if (IsInCombat())
            {
                UpdateUnits();
                UpdateButtons();

                ResizeScale(CombatTrackerScale);
                ResizeWidth(CombatTrackerWidth);

                if (!_enabled)
                {
                    _enabled = true;
                    _canvasGroup.DOFade(1f, 0.5f).SetUpdate(true);
                    _body.DOAnchorPosY(0f, 0.5f, false).SetUpdate(true);
                }
            }
            else
            {
                if (_enabled)
                {
                    _enabled = false;
                    _canvasGroup.DOFade(0f, 0.5f).SetUpdate(true);
                    _body.DOAnchorPosY(_body.rect.height, 0.5f, false).SetUpdate(true);
                    ClearUnits();
                    HoveringUnit = null;
                }
            }
        }

        private void HandleClickFiveFootStep()
        {
            Mod.Core.Combat.CurrentTurn?.CommandToggleFiveFootStep();
        }

        private void HandleClickDelay()
        {
            _buttonDelay.IsPressed = !_buttonDelay.IsPressed;
        }

        private void HandleClickEndTurn()
        {
            Mod.Core.Combat.CurrentTurn?.CommandEndTurn();
        }

        private bool HandleClickUnitButton(UnitEntityData unit)
        {
            if (_buttonDelay.IsPressed)
            {
                Mod.Core.Combat.CurrentTurn?.CommandDelay(unit);
                _buttonDelay.IsPressed = false;
                return false;
            }
            else
            {
                return true;
            }
        }

        private void HandleEnterUnitButton(UnitEntityData unit)
        {
            HoveringUnit = unit;
        }

        private void HandleExitUnitButton(UnitEntityData unit)
        {
            if (HoveringUnit == unit)
            {
                HoveringUnit = null;
            }
        }

        private void UpdateButtons()
        {
            TurnController currentTurn = Mod.Core.Combat.CurrentTurn;

            // 5-foot step button
            if (currentTurn != null)
            {
                _buttonFiveFoorStep.IsInteractable = currentTurn.CanToggleFiveFootStep();
                _buttonFiveFoorStep.IsPressed = currentTurn.EnabledFiveFootStep;
            }
            else
            {
                _buttonFiveFoorStep.IsInteractable = false;
                _buttonFiveFoorStep.IsPressed = false;
            }

            // delay button
            if (currentTurn != null && currentTurn.CanDelay())
            {
                _buttonDelay.IsInteractable = true;
            }
            else
            {
                _buttonDelay.IsInteractable = false;
                _buttonDelay.IsPressed = false;
            }

            // end button
            _buttonEndTurn.IsInteractable = currentTurn != null && currentTurn.CanEndTurn();
        }

        private void UpdateUnits()
        {
            UnitEntityData currentUnit = Mod.Core.Combat.CurrentTurn?.Unit;
            int oldCount = _unitButtonDic.Count;
            int newCount = 0;
            List<UnitButtonManager> newUnitButtons = new List<UnitButtonManager>();
            bool isDirty = false;

            // renew elements
            foreach (UnitEntityData unit in Mod.Core.Combat.SortedUnits)
            {
                if (newCount >= CombatTrackerMaxUnits)
                {
                    break;
                }

                if (!DoNotShowInvisibleUnitOnCombatTracker || unit.IsVisibleForPlayer || unit == currentUnit)
                {
                    newUnitButtons.Add(EnsureUnit(unit, newCount++, ref isDirty));
                }
            }

            if (newCount != oldCount)
            {
                // window size should have changed
                ResizeUnits(newCount);
                isDirty = true;
            }

            if (isDirty)
            {
                // remove disabled button
                foreach (UnitButtonManager button in _unitButtonDic.Values.Except(newUnitButtons).ToList())
                {
                    RemoveUnit(button);
                }

                // do move
                foreach (UnitButtonManager button in _unitButtonDic.Values.OrderBy(button => button.Index))
                {
                    button.transform.SetSiblingIndex(button.Index);
                    button.transform.DOLocalMoveY(-(UNIT_BUTTON_HEIGHT + UNIT_BUTTON_SPACE) * button.Index, 1f).SetUpdate(true);
                }
            }
        }

        private void ClearUnits()
        {
            foreach (UnitButtonManager unitButton in _unitButtonDic.Values)
            {
                unitButton.SafeDestroy();
            }
            _unitButtonDic.Clear();
        }

        private UnitButtonManager EnsureUnit(UnitEntityData unit, int index, ref bool isDirty)
        {
            if (!_unitButtonDic.TryGetValue(unit, out UnitButtonManager button))
            {
                if (!_unitButtonTemplate)
                {
                    _unitButtonTemplate = UnitButtonManager.CreateObject();
                    _unitButtonTemplate.gameObject.SetActive(false);
                    DontDestroyOnLoad(_unitButtonTemplate.gameObject);
                }

                button = Instantiate(_unitButtonTemplate);
                button.transform.SetParent(_unitButtons.transform, false);
                button.transform.localPosition = new Vector3(0f, -(UNIT_BUTTON_HEIGHT + UNIT_BUTTON_SPACE) * index, 0f);
                button.gameObject.SetActive(true);
                button.Index = index;
                button.Unit = unit;
                button.OnClick += HandleClickUnitButton;
                button.OnEnter += HandleEnterUnitButton;
                button.OnExit += HandleExitUnitButton;

                _unitButtonDic.Add(unit, button);
                isDirty = true;
            }
            else if (button.Index != index)
            {
                button.Index = index;
                isDirty = true;
            }
            return button;
        }

        private void RemoveUnit(UnitButtonManager unitButton)
        {
            _unitButtonDic.Remove(unitButton.Unit);
            unitButton.SafeDestroy();
        }

        private void ResizeScale(float scale)
        {
            if (_scale != scale)
            {
                _scale = scale;
                _body.localScale = new Vector3(scale, scale, scale);
            }
        }

        private void ResizeWidth(float width)
        {
            if (_width != width)
            {
                _width = width;
                _body.sizeDelta = new Vector2(width, _body.sizeDelta.y);
                SetPadding(
                    (int)(width * DEFAULT_BLOCK_PADDING.x / DEFAULT_BLOCK_SIZE.x / 2f),
                    _bodyLayoutGroup.padding.top);
            }
        }

        private void ResizeUnits(int unitsCount)
        {
            float height = (UNIT_BUTTON_HEIGHT + UNIT_BUTTON_SPACE) * unitsCount - UNIT_BUTTON_SPACE;
            _unitButtons.sizeDelta = new Vector2(_unitButtons.sizeDelta.x, height);
            SetPadding(
                _bodyLayoutGroup.padding.right, 
                (int)((height + UNIT_BUTTON_HEIGHT * 2 + UNIT_BUTTON_SPACE) * 
                DEFAULT_BLOCK_PADDING.y / (DEFAULT_BLOCK_SIZE.y - DEFAULT_BLOCK_PADDING.y) / 2f));
        }

        private void SetPadding(int x, int y)
        {
            _bodyLayoutGroup.padding = new RectOffset(x, x, y, y);
        }

        private class ButtonWrapper
        {
            private bool _isPressed;

            private readonly Color _enableColor = Color.white;
            private readonly Color _disableColor = new Color(0.7f, 0.8f, 1f);

            private readonly ButtonPF _button;
            private readonly TextMeshProUGUI _textMesh;
            private readonly Image _image;
            private readonly Sprite _defaultSprite;
            private readonly SpriteState _defaultSpriteState;
            private readonly SpriteState _pressedSpriteState;

            public bool IsInteractable {
                get => _button.interactable;
                set {
                    if (_button.interactable != value)
                    {
                        _button.interactable = value;
                        _textMesh.color = value ? _enableColor : _disableColor;
                    }
                }
            }

            public bool IsPressed {
                get => _isPressed;
                set {
                    if (_isPressed != value)
                    {
                        _isPressed = value;
                        if (value)
                        {
                            _button.spriteState = _pressedSpriteState;
                            _image.sprite = _pressedSpriteState.pressedSprite;
                        }
                        else
                        {
                            _button.spriteState = _defaultSpriteState;
                            _image.sprite = _defaultSprite;
                        }
                    }
                }
            }

            public ButtonWrapper(ButtonPF button, string text, Action onClick)
            {
                _button = button;
                _button.onClick = new Button.ButtonClickedEvent();
                _button.onClick.AddListener(new UnityAction(onClick));
                _textMesh = _button.GetComponentInChildren<TextMeshProUGUI>();
                _textMesh.text = text;
                _textMesh.color = _button.interactable ? _enableColor : _disableColor;
                _image = _button.gameObject.GetComponent<Image>();
                _defaultSprite = _image.sprite;
                _defaultSpriteState = _button.spriteState;
                _pressedSpriteState = _defaultSpriteState;
                _pressedSpriteState.disabledSprite = _pressedSpriteState.pressedSprite;
                _pressedSpriteState.highlightedSprite = _pressedSpriteState.pressedSprite;
            }
        }
    }
}