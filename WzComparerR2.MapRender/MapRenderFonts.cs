﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Microsoft.Xna.Framework.Graphics;
using WzComparerR2.Rendering;

namespace WzComparerR2.MapRender
{
    public class MapRenderFonts : IDisposable
    {
        public MapRenderFonts(GraphicsDevice graphicsDevice)
        {
            this.fonts = new Dictionary<string, XnaFont>();
            this.graphicsDevice = graphicsDevice;
            fonts["default"] = new XnaFont(graphicsDevice, "돋움", 12f);
            fonts["npcName"] = fonts["default"];
            fonts["npcDesc"] = new XnaFont(graphicsDevice, "돋움", 13f);
            fonts["mobName"] = fonts["default"];
            fonts["mobLevel"] = new XnaFont(graphicsDevice, "돋움", 9f);
            fonts["mapName"] = new XnaFont(graphicsDevice, new Font("돋움", 12f, FontStyle.Bold, GraphicsUnit.Pixel));
            fonts["tooltipTitle"] = new XnaFont(graphicsDevice, new Font("돋움", 14f, FontStyle.Bold, GraphicsUnit.Pixel));
            fonts["tooltipContent"] = fonts["mobName"];
        }

        Dictionary<string, XnaFont> fonts;
        GraphicsDevice graphicsDevice;

        public GraphicsDevice GraphicsDevice
        {
            get { return graphicsDevice; }
        }

        protected XnaFont this[string key]
        {
            get
            {
                XnaFont font;
                this.fonts.TryGetValue(key, out font);
                return font;
            }
        }

        public XnaFont DefaultFont
        {
            get { return this["default"]; }
        }

        public XnaFont NpcNameFont
        {
            get { return this["npcName"]; }
        }

        public XnaFont NpcDescFont
        {
            get { return this["npcDesc"]; }
        }

        public XnaFont MobNameFont
        {
            get { return this["mobName"]; }
        }

        public XnaFont MobLevelFont
        {
            get { return this["mobLevel"]; }
        }

        public XnaFont MapNameFont
        {
            get { return this["mapName"]; }
        }

        public XnaFont TooltipTitleFont
        {
            get { return this["tooltipTitle"]; }
        }

        public XnaFont TooltipContentFont
        {
            get { return this["tooltipContent"]; }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var kv in fonts)
                {
                    kv.Value.Dispose();
                }
            }
        }
    }
}
