﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DevComponents.DotNetBar;
using DevComponents.Editors;
using DevComponents.DotNetBar.Controls;
using WzComparerR2.Common;
using WzComparerR2.WzLib;
using WzComparerR2.PluginBase;

namespace WzComparerR2.Avatar.UI
{
    internal partial class AvatarForm : DevComponents.DotNetBar.OfficeForm
    {
        public AvatarForm()
        {
            InitializeComponent();
            this.avatar = new AvatarCanvas();
            this.animator = new Animator();
            btnReset_Click(btnReset, EventArgs.Empty);
            FillWeaponIdx();
            FillEarSelection();

#if !DEBUG
            buttonItem1.Visible = false;
#endif
        }

        public SuperTabControlPanel GetTabPanel()
        {
            this.TopLevel = false;
            this.Dock = DockStyle.Fill;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            var pnl = new SuperTabControlPanel();
            pnl.Controls.Add(this);
            pnl.Padding = new System.Windows.Forms.Padding(1);
            this.Visible = true;
            return pnl;
        }

        public Entry PluginEntry { get; set; }

        AvatarCanvas avatar;
        bool inited;
        string partsTag;
        bool suspendUpdate;
        bool needUpdate;
        Animator animator;

        BackgroundWorker worker;
        ProgressDialog progressDialog;

        /// <summary>
        /// wz1节点选中事件。
        /// </summary>
        public void OnSelectedNode1Changed(object sender, WzNodeEventArgs e)
        {
            if (PluginEntry.Context.SelectedTab != PluginEntry.Tab || e.Node == null
                || this.btnLock.Checked)
            {
                return;
            }

            Wz_File file = e.Node.GetNodeWzFile();
            if (file == null)
            {
                return;
            }

            switch (file.Type)
            {
                case Wz_Type.Character: //读取装备
                    Wz_Image wzImg = e.Node.GetValue<Wz_Image>();
                    if (wzImg != null && wzImg.TryExtract())
                    {
                        this.SuspendUpdateDisplay();
                        LoadPart(wzImg.Node);
                        this.ResumeUpdateDisplay();
                    }
                    break;
            }
        }

        public void OnWzClosing(object sender, WzStructureEventArgs e)
        {
            bool hasChanged = false;
            for (int i = 0; i < avatar.Parts.Length; i++)
            {
                var part = avatar.Parts[i];
                if (part != null)
                {
                    var wzFile = part.Node.GetNodeWzFile();
                    if (wzFile != null && e.WzStructure.wz_files.Contains(wzFile))//将要关闭文件 移除
                    {
                        avatar.Parts[i] = null;
                        hasChanged = true;
                    }
                }
            }

            if (hasChanged)
            {
                this.FillAvatarParts();
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 初始化纸娃娃资源。
        /// </summary>
        private bool AvatarInit()
        {
            this.inited = this.avatar.LoadZ()
                && this.avatar.LoadActions()
                && this.avatar.LoadEmotions();

            if (this.inited)
            {
                this.FillBodyAction();
                this.FillEmotion();
            }
            return this.inited;
        }

        /// <summary>
        /// 加载装备部件。
        /// </summary>
        /// <param name="imgNode"></param>
        private void LoadPart(Wz_Node imgNode)
        {
            if (!this.inited && !this.AvatarInit() && imgNode == null)
            {
                return;
            }

            AvatarPart part = this.avatar.AddPart(imgNode);
            if (part != null)
            {
                OnNewPartAdded(part);
                FillAvatarParts();
                UpdateDisplay();
            }
        }

        private void OnNewPartAdded(AvatarPart part)
        {
            if (part == null)
            {
                return;
            }

            if (part == avatar.Body) //同步head
            {
                int headID = 10000 + part.ID.Value % 10000;
                if (avatar.Head == null || avatar.Head.ID != headID)
                {
                    var headImgNode = PluginBase.PluginManager.FindWz(string.Format("Character\\{0:D8}.img", headID));
                    if (headImgNode != null)
                    {
                        this.avatar.AddPart(headImgNode);
                    }
                }
            }
            else if (part == avatar.Head) //同步body
            {
                int bodyID = part.ID.Value % 10000;
                if (avatar.Body == null || avatar.Body.ID != bodyID)
                {
                    var bodyImgNode = PluginBase.PluginManager.FindWz(string.Format("Character\\{0:D8}.img", bodyID));
                    if (bodyImgNode != null)
                    {
                        this.avatar.AddPart(bodyImgNode);
                    }
                }
            }
            else if (part == avatar.Face) //同步表情
            {
                this.avatar.LoadEmotions();
                FillEmotion();
            }
            else if (part == avatar.Taming) //同步座驾动作
            {
                this.avatar.LoadTamingActions();
                FillTamingAction();
                SetTamingDefaultBodyAction();
                SetTamingDefault();
            }
            else if (part == avatar.Weapon) //同步武器类型
            {
                FillWeaponTypes();
            }
            else if (part == avatar.Pants || part == avatar.Coat) //隐藏套装
            {
                if (avatar.Longcoat != null)
                {
                    avatar.Longcoat.Visible = false;
                }
            }
            else if (part == avatar.Longcoat) //还是。。隐藏套装
            {
                if (avatar.Pants != null && avatar.Pants.Visible
                    || avatar.Coat != null && avatar.Coat.Visible)
                {
                    avatar.Longcoat.Visible = false;
                }
            }
        }

        private void SuspendUpdateDisplay()
        {
            this.suspendUpdate = true;
            this.needUpdate = false;
        }

        private void ResumeUpdateDisplay()
        {
            if (this.suspendUpdate)
            {
                this.suspendUpdate = false;
                if (this.needUpdate)
                {
                    this.UpdateDisplay();
                }
            }
        }

        /// <summary>
        /// 更新画布。
        /// </summary>
        private void UpdateDisplay()
        {
            if (suspendUpdate)
            {
                this.needUpdate = true;
                return;
            }

            string newPartsTag = GetAllPartsTag();
            if (this.partsTag != newPartsTag)
            {
                this.partsTag = newPartsTag;
                this.avatarContainer1.ClearAllCache();
            }

            ComboItem selectedItem;
            //同步角色动作
            selectedItem = this.cmbActionBody.SelectedItem as ComboItem;
            this.avatar.ActionName = selectedItem != null ? selectedItem.Text : null;
            //同步表情
            selectedItem = this.cmbEmotion.SelectedItem as ComboItem;
            this.avatar.EmotionName = selectedItem != null ? selectedItem.Text : null;
            //同步骑宠动作
            selectedItem = this.cmbActionTaming.SelectedItem as ComboItem;
            this.avatar.TamingActionName = selectedItem != null ? selectedItem.Text : null;

            //获取动作帧
            selectedItem = this.cmbBodyFrame.SelectedItem as ComboItem;
            int bodyFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;
            selectedItem = this.cmbEmotionFrame.SelectedItem as ComboItem;
            int emoFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;
            selectedItem = this.cmbTamingFrame.SelectedItem as ComboItem;
            int tamingFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;

            //获取武器状态
            selectedItem = this.cmbWeaponType.SelectedItem as ComboItem;
            this.avatar.WeaponType = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : 0;

            selectedItem = this.cmbWeaponIdx.SelectedItem as ComboItem;
            this.avatar.WeaponIndex = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : 0;

            //获取耳朵状态
            selectedItem = this.cmbEar.SelectedItem as ComboItem;
            this.avatar.EarType = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : 0;

            string actionTag = string.Format("{0}:{1},{2}:{3},{4}:{5},{6},{7},{8},{9},{10}",
                this.avatar.ActionName,
                bodyFrame,
                this.avatar.EmotionName,
                emoFrame,
                this.avatar.TamingActionName,
                tamingFrame,
                this.avatar.HairCover ? 1 : 0,
                this.avatar.ShowHairShade ? 1 : 0,
                this.avatar.EarType,
                this.avatar.WeaponType,
                this.avatar.WeaponIndex);

            if (!avatarContainer1.HasCache(actionTag))
            {
                try
                {
                    var actionFrames = avatar.GetActionFrames(avatar.ActionName);
                    ActionFrame f = null;
                    if (bodyFrame > -1 && bodyFrame < actionFrames.Length)
                    {
                        f = actionFrames[bodyFrame];
                    }

                    var bone = avatar.CreateFrame(bodyFrame, emoFrame, tamingFrame);
                    var layers = avatar.CreateFrameLayers(bone);
                    avatarContainer1.AddCache(actionTag, layers);
                }
                catch
                {
                }
            }

            avatarContainer1.SetKey(actionTag);
        }

        private string GetAllPartsTag()
        {
            string[] partsID = new string[avatar.Parts.Length];
            for (int i = 0; i < avatar.Parts.Length; i++)
            {
                var part = avatar.Parts[i];
                if (part != null && part.Visible)
                {
                    partsID[i] = part.ID.ToString();
                }
            }
            return string.Join(",", partsID);
        }

        private void buttonItem1_Click(object sender, EventArgs e)
        {
            AddPart("Character\\00002000.img");
            AddPart("Character\\00012000.img");
            AddPart("Character\\Face\\00020000.img");
            AddPart("Character\\Hair\\00030000.img");
            AddPart("Character\\Coat\\01040036.img");
            AddPart("Character\\Pants\\01060026.img");
            FillAvatarParts();
            UpdateDisplay();
        }

        void AddPart(string imgPath)
        {
            Wz_Node imgNode = PluginManager.FindWz(imgPath);
            if (imgNode != null)
            {
                this.avatar.AddPart(imgNode);
            }
        }

        private void SelectBodyAction(string actionName)
        {
            for (int i = 0; i < cmbActionBody.Items.Count; i++)
            {
                ComboItem item = cmbActionBody.Items[i] as ComboItem;
                if (item != null && item.Text == actionName)
                {
                    cmbActionBody.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectEmotion(string emotionName)
        {
            for (int i = 0; i < cmbEmotion.Items.Count; i++)
            {
                ComboItem item = cmbEmotion.Items[i] as ComboItem;
                if (item != null && item.Text == emotionName)
                {
                    cmbEmotion.SelectedIndex = i;
                    return;
                }
            }
        }

        #region 同步界面
        private void FillBodyAction()
        {
            var oldSelection = cmbActionBody.SelectedItem as ComboItem;
            int? newSelection = null;
            cmbActionBody.BeginUpdate();
            cmbActionBody.Items.Clear();
            foreach (var action in this.avatar.Actions)
            {
                ComboItem cmbItem = new ComboItem(action.Name);
                switch (action.Level)
                {
                    case 0:
                        cmbItem.FontStyle = FontStyle.Bold;
                        cmbItem.ForeColor = Color.Indigo;
                        break;

                    case 1:
                        cmbItem.ForeColor = Color.Indigo;
                        break;
                }
                cmbItem.Tag = action;
                cmbActionBody.Items.Add(cmbItem);

                if (newSelection == null && oldSelection != null)
                {
                    if (cmbItem.Text == oldSelection.Text)
                    {
                        newSelection = cmbActionBody.Items.Count - 1;
                    }
                }
            }

            if (cmbActionBody.Items.Count > 0)
            {
                cmbActionBody.SelectedIndex = newSelection ?? 0;
            }

            cmbActionBody.EndUpdate();
        }

        private void FillEmotion()
        {
            FillComboItems(cmbEmotion, avatar.Emotions);
        }

        private void FillTamingAction()
        {
            FillComboItems(cmbActionTaming, avatar.TamingActions);
        }

        private void FillWeaponTypes()
        {
            List<int> weaponTypes = avatar.GetCashWeaponTypes();
            FillComboItems(cmbWeaponType, weaponTypes.ConvertAll(i => i.ToString()));
        }

        private void SetTamingDefaultBodyAction()
        {
            string actionName;
            var tamingAction = (this.cmbActionTaming.SelectedItem as ComboItem)?.Text;
            switch (tamingAction)
            {
                case "ladder":
                case "rope":
                    actionName = tamingAction;
                    break;
                default:
                    actionName = "sit";
                    break;
            }
            SelectBodyAction(actionName);
        }

        private void SetTamingDefault()
        {
            if (this.avatar.Taming != null)
            {
                var tamingAction =  (this.cmbActionTaming.SelectedItem as ComboItem)?.Text;
                if (tamingAction != null)
                {
                    string forceAction = this.avatar.Taming.Node.FindNodeByPath($@"characterAction\{tamingAction}").GetValueEx<string>(null);
                    if (forceAction != null)
                    {
                        this.SelectBodyAction(forceAction);
                    }

                    string forceEmotion = this.avatar.Taming.Node.FindNodeByPath($@"characterEmotion\{tamingAction}").GetValueEx<string>(null);
                    if (forceEmotion != null)
                    {
                        this.SelectEmotion(forceEmotion);
                    }
                }
            }
        }

        /// <summary>
        /// 更新当前显示部件列表。
        /// </summary>
        private void FillAvatarParts()
        {
            itemPanel1.BeginUpdate();
            itemPanel1.Items.Clear();
            foreach (var part in avatar.Parts)
            {
                if (part != null)
                {
                    var btn = new AvatarPartButtonItem();
                    var stringLinker = this.PluginEntry.Context.DefaultStringLinker;
                    StringResult sr;
                    string text;
                    if (part.ID != null && stringLinker.StringEqp.TryGetValue(part.ID.Value, out sr))
                    {
                        text = string.Format("{0}\r\n{1}", sr.Name, part.ID);
                    }
                    else
                    {
                        text = string.Format("{0}\r\n{1}", "(null)", part.ID == null ? "-" : part.ID.ToString());
                    }
                    btn.Text = text;
                    btn.SetIcon(part.Icon.Bitmap);
                    btn.Tag = part;
                    btn.Checked = part.Visible;
                    btn.btnItemShow.Click += BtnItemShow_Click;
                    btn.btnItemDel.Click += BtnItemDel_Click;
                    btn.CheckedChanged += Btn_CheckedChanged;
                    itemPanel1.Items.Add(btn);
                }
            }
            itemPanel1.EndUpdate();
        }

        private void BtnItemShow_Click(object sender, EventArgs e)
        {
            var btn = (sender as BaseItem).Parent as AvatarPartButtonItem;
            if (btn != null)
            {
                btn.Checked = !btn.Checked;
            }
        }

        private void BtnItemDel_Click(object sender, EventArgs e)
        {
            var btn = (sender as BaseItem).Parent as AvatarPartButtonItem;
            if (btn != null)
            {
                var part = btn.Tag as AvatarPart;
                if (part != null)
                {
                    int index = Array.IndexOf(this.avatar.Parts, part);
                    if (index > -1)
                    {
                        this.avatar.Parts[index] = null;
                        this.FillAvatarParts();
                        this.UpdateDisplay();
                    }
                }
            }
        }

        private void Btn_CheckedChanged(object sender, EventArgs e)
        {
            var btn = sender as AvatarPartButtonItem;
            if (btn != null)
            {
                var part = btn.Tag as AvatarPart;
                if (part != null)
                {
                    part.Visible = btn.Checked;
                    this.UpdateDisplay();
                }
            }
        }

        private void FillBodyActionFrame()
        {
            ComboItem actionItem = cmbActionBody.SelectedItem as ComboItem;
            if (actionItem != null)
            {
                var frames = avatar.GetActionFrames(actionItem.Text);
                FillComboItems(cmbBodyFrame, frames);
            }
            else
            {
                cmbBodyFrame.Items.Clear();
            }
        }

        private void FillEmotionFrame()
        {
            ComboItem emotionItem = cmbEmotion.SelectedItem as ComboItem;
            if (emotionItem != null)
            {
                var frames = avatar.GetFaceFrames(emotionItem.Text);
                FillComboItems(cmbEmotionFrame, frames);
            }
            else
            {
                cmbEmotionFrame.Items.Clear();
            }
        }

        private void FillTamingActionFrame()
        {
            ComboItem actionItem = cmbActionTaming.SelectedItem as ComboItem;
            if (actionItem != null)
            {
                var frames = avatar.GetTamingFrames(actionItem.Text);
                FillComboItems(cmbTamingFrame, frames);
            }
            else
            {
                cmbTamingFrame.Items.Clear();
            }
        }

        private void FillWeaponIdx()
        {
            FillComboItems(cmbWeaponIdx, 0, 4);
        }

        private void FillEarSelection()
        {
            FillComboItems(cmbEar, 0, 4);
        }

        private void FillComboItems(ComboBoxEx comboBox, int start, int count)
        {
            List<ComboItem> items = new List<ComboItem>(count);
            for (int i = 0; i < count; i++)
            {
                ComboItem item = new ComboItem();
                item.Text = (start + i).ToString();
                items.Add(item);
            }
            FillComboItems(comboBox, items);
        }

        private void FillComboItems(ComboBoxEx comboBox, IEnumerable<string> items)
        {
            List<ComboItem> _items = new List<ComboItem>();
            foreach (var itemText in items)
            {
                ComboItem item = new ComboItem();
                item.Text = itemText;
                _items.Add(item);
            }
            FillComboItems(comboBox, _items);
        }

        private void FillComboItems(ComboBoxEx comboBox, IEnumerable<ActionFrame> frames)
        {
            List<ComboItem> items = new List<ComboItem>();
            int i = 0;
            foreach (var f in frames)
            {
                ComboItem item = new ComboItem();
                item.Text = (i++).ToString();
                item.Tag = Math.Abs(f.Delay);
                items.Add(item);
            }
            FillComboItems(comboBox, items);
        }

        private void FillComboItems(ComboBoxEx comboBox, IEnumerable<ComboItem> items)
        {
            //保持原有选项
            var oldSelection = comboBox.SelectedItem as ComboItem;
            int? newSelection = null;
            comboBox.BeginUpdate();
            comboBox.Items.Clear();

            foreach (var item in items)
            {
                comboBox.Items.Add(item);

                if (newSelection == null && oldSelection != null)
                {
                    if (item.Text == oldSelection.Text)
                    {
                        newSelection = comboBox.Items.Count - 1;
                    }
                }
            }

            //恢复原有选项
            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = newSelection ?? 0;
            }

            comboBox.EndUpdate();
        }
        #endregion

        private void cmbActionBody_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SuspendUpdateDisplay();
            FillBodyActionFrame();
            this.ResumeUpdateDisplay();
            UpdateDisplay();
        }

        private void cmbEmotion_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SuspendUpdateDisplay();
            FillEmotionFrame();
            this.ResumeUpdateDisplay();
            UpdateDisplay();
        }

        private void cmbActionTaming_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.SuspendUpdateDisplay();
            FillTamingActionFrame();
            SetTamingDefaultBodyAction();
            SetTamingDefault();
            this.ResumeUpdateDisplay();
            UpdateDisplay();
        }

        private void cmbBodyFrame_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void cmbEmotionFrame_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void cmbTamingFrame_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void cmbWeaponType_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void cmbWeaponIdx_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void cmbEar_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void chkBodyPlay_CheckedChanged(object sender, EventArgs e)
        {
            if (chkBodyPlay.Checked)
            {
                if (!this.timer1.Enabled)
                {
                    AnimateStart();
                }

                var item = cmbBodyFrame.SelectedItem as ComboItem;
                int? delay;
                if (item != null && ((delay = item.Tag as int?) != null) && delay.Value >= 0)
                {
                    this.animator.BodyDelay = delay.Value;
                }
            }
            else
            {
                this.animator.BodyDelay = -1;
                TimerEnabledCheck();
            }
        }

        private void chkEmotionPlay_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEmotionPlay.Checked)
            {
                if (!this.timer1.Enabled)
                {
                    AnimateStart();
                }
                var item = cmbEmotionFrame.SelectedItem as ComboItem;
                int? delay;
                if (item != null && ((delay = item.Tag as int?) != null) && delay.Value >= 0)
                {
                    this.animator.EmotionDelay = delay.Value;
                }
            }
            else
            {
                this.animator.EmotionDelay = -1;
                TimerEnabledCheck();
            }
        }

        private void chkTamingPlay_CheckedChanged(object sender, EventArgs e)
        {
            if (chkTamingPlay.Checked)
            {
                if (!this.timer1.Enabled)
                {
                    AnimateStart();
                }
                var item = cmbTamingFrame.SelectedItem as ComboItem;
                int? delay;
                if (item != null && ((delay = item.Tag as int?) != null) && delay.Value >= 0)
                {
                    this.animator.TamingDelay = delay.Value;
                }
            }
            else
            {
                this.animator.TamingDelay = -1;
                TimerEnabledCheck();
            }
        }

        private void chkHairCover_CheckedChanged(object sender, EventArgs e)
        {
            avatar.HairCover = chkHairCover.Checked;
            UpdateDisplay();
        }

        private void chkHairShade_CheckedChanged(object sender, EventArgs e)
        {
            avatar.ShowHairShade = chkHairShade.Checked;
            UpdateDisplay();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.animator.Elapse(timer1.Interval);
            this.AnimateUpdate();
            int interval = this.animator.NextFrameDelay;

            if (interval <= 0)
            {
                this.timer1.Stop();
            }
            else
            {
                this.timer1.Interval = interval;
            }
        }

        private void AnimateUpdate()
        {
            this.SuspendUpdateDisplay();

            if (this.animator.BodyDelay == 0 && FindNextFrame(cmbBodyFrame))
            {
                this.animator.BodyDelay = (int)(cmbBodyFrame.SelectedItem as ComboItem).Tag;
            }

            if (this.animator.EmotionDelay == 0 && FindNextFrame(cmbEmotionFrame))
            {
                this.animator.EmotionDelay = (int)(cmbEmotionFrame.SelectedItem as ComboItem).Tag;
            }

            if (this.animator.TamingDelay == 0 && FindNextFrame(cmbTamingFrame))
            {
                this.animator.TamingDelay = (int)(cmbTamingFrame.SelectedItem as ComboItem).Tag;
            }

            this.ResumeUpdateDisplay();
        }

        private void AnimateStart()
        {
            TimerEnabledCheck();
            if (timer1.Enabled)
            {
                AnimateUpdate();
            }
        }

        private void TimerEnabledCheck()
        {
            if (chkBodyPlay.Checked || chkEmotionPlay.Checked || chkTamingPlay.Checked)
            {
                if (!this.timer1.Enabled)
                {
                    this.timer1.Interval = 1;
                    this.timer1.Start();
                }
            }
            else
            {
                AnimateStop();
            }
        }

        private void AnimateStop()
        {
            chkBodyPlay.Checked = false;
            chkEmotionPlay.Checked = false;
            chkTamingPlay.Checked = false;
            this.timer1.Stop();
        }

        private bool FindNextFrame(ComboBoxEx cmbFrames)
        {
            ComboItem item = cmbFrames.SelectedItem as ComboItem;
            if (item == null)
            {
                if (cmbFrames.Items.Count > 0)
                {
                    cmbFrames.SelectedIndex = 0;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            int selectedIndex = cmbFrames.SelectedIndex;
            int i = selectedIndex;
            do
            {
                i = (++i) % cmbFrames.Items.Count;
                item = cmbFrames.Items[i] as ComboItem;
                if (item != null && item.Tag is int)
                {
                    int delay = (int)item.Tag;
                    if (delay > 0)
                    {
                        cmbFrames.SelectedIndex = i;
                        return true;
                    }
                }
            }
            while (i != selectedIndex);

            return false;
        }

        private void btnCode_Click(object sender, EventArgs e)
        {
            var dlg = new AvatarCodeForm();
            string code = GetAllPartsTag();
            dlg.CodeText = code;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (dlg.CodeText != code && !string.IsNullOrEmpty(dlg.CodeText))
                {
                    LoadCode(dlg.CodeText, dlg.LoadType);
                }
            }
        }

        private void btnMale_Click(object sender, EventArgs e)
        {
            if (MessageBoxEx.Show("Add a basic male character?", "Confirm") == DialogResult.OK)
            {
                LoadCode("2000,12000,20000,30000,1040036,1060026", 0);
            }
        }

        private void btnFemale_Click(object sender, EventArgs e)
        {
            if (MessageBoxEx.Show("Add a basic female character?", "Confirm") == DialogResult.OK)
            {
                LoadCode("2000,12000,21000,31000,1041046,1061039", 0);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            this.avatarContainer1.Origin = new Point(this.avatarContainer1.Width / 2, this.avatarContainer1.Height / 2 + 40);
            this.avatarContainer1.Invalidate();
        }

        private void LoadCode(string code, int loadType)
        {
            //解析
            var matches = Regex.Matches(code, @"(\d+)([,\s]|$)");
            if (matches.Count <= 0)
            {
                MessageBoxEx.Show("아이템 코드에 해당되는 아이템이 없습니다.", "오류");
                return;
            }

            var characWz = PluginManager.FindWz(Wz_Type.Character);
            if (characWz == null)
            {
                MessageBoxEx.Show("Character.wz 파일을 열 수 없습니다.", "오류");
                return;
            }

            //试图初始化
            if (!this.inited && !this.AvatarInit())
            {
                MessageBoxEx.Show("아바타 플러그인을 초기화할 수 없습니다.", "오류");
                return;
            }

            if (loadType == 0) //先清空。。
            {
                Array.Clear(this.avatar.Parts, 0, this.avatar.Parts.Length);
            }

            List<int> failList = new List<int>();

            foreach (Match m in matches)
            {
                int gearID;
                if (Int32.TryParse(m.Result("$1"), out gearID))
                {
                    Wz_Node imgNode = FindNodeByGearID(characWz, gearID);
                    if (imgNode != null)
                    {
                        var part = this.avatar.AddPart(imgNode);
                        OnNewPartAdded(part);
                    }
                    else
                    {
                        failList.Add(gearID);
                    }
                }
            }

            //刷新
            this.FillAvatarParts();
            this.UpdateDisplay();

            //其他提示
            if (failList.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("해당 아이템 코드를 찾을 수 없습니다 : ");
                foreach (var gearID in failList)
                {
                    sb.Append("  ").AppendLine(gearID.ToString("D8"));
                }
                MessageBoxEx.Show(sb.ToString(), "오류");
            }

        }

        private Wz_Node FindNodeByGearID(Wz_Node characWz, int id)
        {
            string imgName = id.ToString("D8") + ".img";
            Wz_Node imgNode = null;

            foreach (var node1 in characWz.Nodes)
            {
                if (node1.Text == imgName)
                {
                    imgNode = node1;
                    break;
                }
                else if (node1.Nodes.Count > 0)
                {
                    foreach (var node2 in node1.Nodes)
                    {
                        if (node2.Text == imgName)
                        {
                            imgNode = node2;
                            break;
                        }
                    }
                    if (imgNode != null)
                    {
                        break;
                    }
                }
            }

            if (imgNode != null)
            {
                Wz_Image img = imgNode.GetValue<Wz_Image>();
                if (img != null && img.TryExtract())
                {
                    return img.Node;
                }
            }

            return null;
        }

        private class Animator
        {
            public Animator()
            {
                this.delays = new int[3] { -1, -1, -1 };
            }

            private int[] delays;

            public int NextFrameDelay { get; private set; }

            public int BodyDelay
            {
                get { return this.delays[0]; }
                set
                {
                    this.delays[0] = value;
                    Update();
                }
            }

            public int EmotionDelay
            {
                get { return this.delays[1]; }
                set
                {
                    this.delays[1] = value;
                    Update();
                }
            }

            public int TamingDelay
            {
                get { return this.delays[2]; }
                set
                {
                    this.delays[2] = value;
                    Update();
                }
            }

            public void Elapse(int millisecond)
            {
                for (int i = 0; i < delays.Length; i++)
                {
                    if (delays[i] >= 0)
                    {
                        delays[i] = delays[i] > millisecond ? (delays[i] - millisecond) : 0;
                    }
                }
            }

            private void Update()
            {
                int nextFrame = 0;
                foreach (int delay in this.delays)
                {
                    if (delay > 0)
                    {
                        nextFrame = nextFrame <= 0 ? delay : Math.Min(nextFrame, delay);
                    }
                }
                this.NextFrameDelay = nextFrame;
            }
        }

        private void buttonItem1_Click_1(object sender, EventArgs e)
        {
            this.PluginEntry.btnSetting_Click(sender, e);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            ExportAvatar(sender, e, false);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            ExportAvatar(sender, e, true);
        }

        private void ExportAvatar(object sender, EventArgs e, bool all)
        {
            ComboItem selectedItem;
            //同步角色动作
            selectedItem = this.cmbActionBody.SelectedItem as ComboItem;
            this.avatar.ActionName = selectedItem != null ? selectedItem.Text : null;
            //同步表情
            selectedItem = this.cmbEmotion.SelectedItem as ComboItem;
            this.avatar.EmotionName = selectedItem != null ? selectedItem.Text : null;
            //同步骑宠动作
            selectedItem = this.cmbActionTaming.SelectedItem as ComboItem;
            this.avatar.TamingActionName = selectedItem != null && !all ? selectedItem.Text : null;

            //获取动作帧
            selectedItem = this.cmbBodyFrame.SelectedItem as ComboItem;
            int bodyFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;
            selectedItem = this.cmbEmotionFrame.SelectedItem as ComboItem;
            int emoFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;
            selectedItem = this.cmbTamingFrame.SelectedItem as ComboItem;
            int tamingFrame = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : -1;

            //获取武器状态
            selectedItem = this.cmbWeaponType.SelectedItem as ComboItem;
            this.avatar.WeaponType = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : 0;

            selectedItem = this.cmbWeaponIdx.SelectedItem as ComboItem;
            this.avatar.WeaponIndex = selectedItem != null ? Convert.ToInt32(selectedItem.Text) : 0;

            if (this.avatar.ActionName == null)
            {
                MessageBoxEx.Show("캐릭터가 없습니다.");
                return;
            }

            var config = Config.ImageHandlerConfig.Default;

            if (!all && !(this.chkBodyPlay.Checked || this.chkTamingPlay.Checked))
            {
                var bone = avatar.CreateFrame(bodyFrame, emoFrame, tamingFrame);
                var bmp = avatar.DrawFrame(bone);

                string pngFileName = "avatar"
                    + (string.IsNullOrEmpty(avatar.ActionName) ? "" : ("_" + avatar.ActionName + "(" + bodyFrame + ")"))
                    + (string.IsNullOrEmpty(avatar.EmotionName) ? "" : ("_" + avatar.EmotionName + "(" + emoFrame + ")"))
                    + (string.IsNullOrEmpty(avatar.TamingActionName) ? "" : ("_" + avatar.TamingActionName + "(" + tamingFrame + ")"))
                    + ".png";

                var dlg = new SaveFileDialog();
                dlg.Filter = "PNG (*.png)|*.png|모든 파일 (*.*)|*.*";
                dlg.FileName = pngFileName;
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                pngFileName = dlg.FileName;

                bmp.Bitmap.Save(pngFileName, System.Drawing.Imaging.ImageFormat.Png);

                return;
            }

            var encParams = AnimateEncoderFactory.GetEncoderParams(config.GifEncoder.Value);

            string aniFileName = "avatar"
                    + (string.IsNullOrEmpty(avatar.ActionName) ? "" : ("_" + avatar.ActionName))
                    + (string.IsNullOrEmpty(avatar.EmotionName) ? "" : ("_" + avatar.EmotionName))
                    + (string.IsNullOrEmpty(avatar.TamingActionName) ? "" : ("_" + avatar.TamingActionName))
                    + encParams.FileExtension;

            if (!all)
            {
                var dlg = new SaveFileDialog();

                dlg.Filter = string.Format("{0} (*{1})|*{1}|모든 파일(*.*)|*.*", encParams.FileDescription, encParams.FileExtension);
                dlg.FileName = aniFileName;

                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                aniFileName = dlg.FileName;

                ExportGif(bodyFrame, emoFrame, tamingFrame, aniFileName, config);
                MessageBoxEx.Show("그림 저장 완료: " + aniFileName);
            }
            else
            {
                if (worker?.IsBusy ?? false)
                {
                    MessageBox.Show("다른 내보내기 작업이 진행중입니다.");
                    return;
                }

                FolderBrowserDialog dlg = new FolderBrowserDialog();
                dlg.Description = "내보내고자 하는 폴더를 선택하세요.";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    this.Enabled = false;

                    this.progressDialog = new ProgressDialog(this.Handle);
                    this.progressDialog.Title = "동작 내보내는 중";
                    this.progressDialog.Maximum = (uint)avatar.Actions.Count;
                    this.progressDialog.Value = 0;
                    this.progressDialog.Line1 = string.Format("{0:N0}개 항목 (내보내는 중)", this.progressDialog.Maximum);
                    this.progressDialog.Line2 = string.Format("남은 항목: {0:N0}", this.progressDialog.Maximum);
                    this.progressDialog.Line3 = "남은 시간: 계산 중...";
                    this.progressDialog.ShowDialog(ProgressDialog.PROGDLG.AutoTime);

                    this.worker.RunWorkerAsync(new Tuple<string, int>(dlg.SelectedPath, emoFrame));
                }
            }
        }

        private void ExportAll_DoWork(object sender, DoWorkEventArgs e)
        {
            Tuple<string, int> arg = e.Argument as Tuple<string, int>;
            if (arg != null)
            {
                var config = Config.ImageHandlerConfig.Default;

                var encParams = AnimateEncoderFactory.GetEncoderParams(config.GifEncoder.Value);

                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                for (int i = 0; i < avatar.Actions.Count; i++)
                {
                    if (worker.CancellationPending)
                    {
                        break;
                    }
                    avatar.ActionName = avatar.Actions[i].Name;
                    ExportGif(0, arg.Item2, 0, System.IO.Path.Combine(arg.Item1, avatar.Actions[i].Name.Replace('\\', '.') + encParams.FileExtension), config);
                    worker.ReportProgress(i);
                }

                stopwatch.Stop();
                e.Result = "동작 내보내기 완료: 소요 시간 " + stopwatch.Elapsed.ToString();
            }
        }

        private void ExportAll_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (this.progressDialog.HasUserCancelled)
            {
                this.worker.CancelAsync();
            }
            this.progressDialog.Title = 100 * e.ProgressPercentage / this.progressDialog.Maximum + "% 완료";
            this.progressDialog.Line2 = string.Format("남은 항목: {0:N0}", this.progressDialog.Maximum - e.ProgressPercentage);
            this.progressDialog.Value = (uint)e.ProgressPercentage;
        }

        private void ExportAll_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string result = e.Result as string;
            this.progressDialog.CloseDialog();
            MessageBoxEx.Show(result);
            this.Enabled = true;
        }
        
        private void ExportGif(int bodyFrame, int emoFrame, int tamingFrame, string fileName, Config.ImageHandlerConfig config)
        {
            Gif gif = new Gif();
            var actionFrames = avatar.GetActionFrames(avatar.ActionName);
            var faceFrames = avatar.GetFaceFrames(avatar.EmotionName);
            var tamingFrames = avatar.GetTamingFrames(avatar.TamingActionName);
            if (emoFrame <= -1 || emoFrame >= faceFrames.Length)
            {
                return;
            }
            
            foreach (var frame in string.IsNullOrEmpty(avatar.TamingActionName) ? actionFrames : tamingFrames)
            {
                if (frame.Delay != 0)
                {
                    var bone = string.IsNullOrEmpty(avatar.TamingActionName) ? avatar.CreateFrame(frame, faceFrames[emoFrame], null) : avatar.CreateFrame(actionFrames[bodyFrame], faceFrames[emoFrame], frame);
                    var bmp = avatar.DrawFrame(bone);

                    Point pos = bmp.OpOrigin;
                    pos.Offset(frame.Flip ? new Point(-frame.Move.X, frame.Move.Y) : frame.Move);
                    GifFrame f = new GifFrame(bmp.Bitmap, new Point(-pos.X, -pos.Y), Math.Abs(frame.Delay));
                    gif.Frames.Add(f);
                }
            }

            GifEncoder enc = AnimateEncoderFactory.CreateEncoder(fileName, gif.GetRect().Width, gif.GetRect().Height, config);
            gif.SaveGif(enc, fileName, Color.Transparent);
        }
    }
}