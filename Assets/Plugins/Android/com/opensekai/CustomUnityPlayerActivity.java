package com.opensekai;

import android.content.Intent;
import com.unity3d.player.UnityPlayerActivity;

public class CustomUnityPlayerActivity extends UnityPlayerActivity {
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (!ShareExportHelper.OnActivityResult(requestCode, resultCode, data)) {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }
}
