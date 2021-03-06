﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Kinovea.ScreenManager.Languages;
using System.IO;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public static class DualSnapshoter
    {
        public static void Save(PlayerScreen leftPlayer, PlayerScreen rightPlayer, bool merging)
        {
            string filename = GetFilename(leftPlayer, rightPlayer);
            if (string.IsNullOrEmpty(filename))
                return;

            Bitmap composite;

            Bitmap leftImage = leftPlayer.GetFlushedImage();

            if (!merging)
            {
                Bitmap rightImage = rightPlayer.GetFlushedImage();
                composite = ImageHelper.GetSideBySideComposite(leftImage, rightImage, false, true);
                rightImage.Dispose();
            }
            else
            {
                composite = leftImage;
            }
            
            ImageHelper.Save(filename, composite);

            composite.Dispose();
            
            NotificationCenter.RaiseRefreshFileExplorer(null, false);
        }

        private static string GetFilename(PlayerScreen leftPlayer, PlayerScreen rightPlayer)
        {
            SaveFileDialog dlgSave = new SaveFileDialog();
            dlgSave.Title = ScreenManagerLang.Generic_SaveImage;
            dlgSave.RestoreDirectory = true;
            dlgSave.Filter = ScreenManagerLang.FileFilter_SaveImage;
            dlgSave.FilterIndex = 1;
            dlgSave.FileName = String.Format("{0} - {1}", Path.GetFileNameWithoutExtension(leftPlayer.FilePath), Path.GetFileNameWithoutExtension(rightPlayer.FilePath));

            if (dlgSave.ShowDialog() != DialogResult.OK)
                return null;

            return dlgSave.FileName;
        }
    }
}
