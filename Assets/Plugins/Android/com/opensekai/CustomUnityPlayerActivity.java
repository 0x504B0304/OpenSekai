package com.opensekai;

import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.view.Display;
import android.view.Surface;
import android.view.SurfaceHolder;
import android.view.SurfaceView;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.ViewTreeObserver;
import java.util.HashSet;
import java.util.Set;
import com.unity3d.player.UnityPlayerActivity;

public class CustomUnityPlayerActivity extends UnityPlayerActivity {
    private volatile int maximumFrameRate;
    private boolean frameRateConfigured;
    private final Set<SurfaceHolder> renderSurfaces = new HashSet<>();
    private final ViewTreeObserver.OnGlobalLayoutListener surfaceFinder =
            () -> findRenderSurfaces(getWindow().getDecorView());
    private final SurfaceHolder.Callback surfaceCallback = new SurfaceHolder.Callback() {
        @Override public void surfaceCreated(SurfaceHolder holder) { requestSurfaceRate(holder); }
        @Override public void surfaceChanged(SurfaceHolder holder, int format, int width, int height) {
            requestSurfaceRate(holder);
        }
        @Override public void surfaceDestroyed(SurfaceHolder holder) { }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().getDecorView().getViewTreeObserver().addOnGlobalLayoutListener(surfaceFinder);
    }

    // Called through Unity JNI. Only window/surface mutations need the UI thread.
    public int setMaximumFrameRate(int fps) {
        maximumFrameRate = Math.max(0, fps);
        Display.Mode mode = selectDisplayMode();
        int resolved = fps > 0 ? fps : Math.round(mode.getRefreshRate());
        runOnUiThread(() -> {
            frameRateConfigured = true;
            applyFrameRate();
        });
        return resolved;
    }

    private Display.Mode selectDisplayMode() {
        Display display = getWindowManager().getDefaultDisplay();
        Display.Mode current = display.getMode();
        Display.Mode best = current;
        double bestScore = Double.MAX_VALUE;
        for (Display.Mode mode : display.getSupportedModes()) {
            // Never trade display resolution for refresh rate.
            if (mode.getPhysicalWidth() != current.getPhysicalWidth()
                    || mode.getPhysicalHeight() != current.getPhysicalHeight()) continue;
            float rate = mode.getRefreshRate();
            int cap = maximumFrameRate;
            // Prefer the lowest mode that can present every requested frame evenly.
            // Otherwise prefer the lowest mode above the cap, then the highest below it.
            double multiple = cap > 0 ? rate / cap : 0;
            boolean evenlyDivisible = multiple >= 0.99 && Math.abs(multiple - Math.round(multiple)) < 0.01;
            double score = cap == 0 ? -rate : evenlyDivisible ? rate
                    : rate >= cap - 0.1 ? 10000 + rate : 20000 - rate;
            if (score < bestScore) { bestScore = score; best = mode; }
        }
        return best;
    }

    private void applyFrameRate() {
        if (!frameRateConfigured || isFinishing()) return;
        Display.Mode mode = selectDisplayMode();
        WindowManager.LayoutParams attributes = getWindow().getAttributes();
        if (attributes.preferredDisplayModeId != mode.getModeId()
                || attributes.preferredRefreshRate != mode.getRefreshRate()) {
            attributes.preferredDisplayModeId = mode.getModeId();
            attributes.preferredRefreshRate = mode.getRefreshRate();
            getWindow().setAttributes(attributes);
        }
        findRenderSurfaces(getWindow().getDecorView());
        for (SurfaceHolder holder : renderSurfaces) requestSurfaceRate(holder);
    }

    private void findRenderSurfaces(View view) {
        if (!frameRateConfigured) return;
        if (view instanceof SurfaceView) {
            SurfaceHolder holder = ((SurfaceView) view).getHolder();
            if (renderSurfaces.add(holder)) {
                holder.addCallback(surfaceCallback);
                requestSurfaceRate(holder);
            }
        }
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) findRenderSurfaces(group.getChildAt(i));
        }
    }

    private void requestSurfaceRate(SurfaceHolder holder) {
        if (!frameRateConfigured || Build.VERSION.SDK_INT < 30) return;
        Surface surface = holder.getSurface();
        if (!surface.isValid()) return;
        float rate = maximumFrameRate > 0 ? maximumFrameRate : selectDisplayMode().getRefreshRate();
        try {
            if (Build.VERSION.SDK_INT >= 31) {
                surface.setFrameRate(rate, Surface.FRAME_RATE_COMPATIBILITY_DEFAULT,
                        Surface.CHANGE_FRAME_RATE_ONLY_IF_SEAMLESS);
            } else {
                surface.setFrameRate(rate, Surface.FRAME_RATE_COMPATIBILITY_DEFAULT);
            }
        } catch (IllegalArgumentException | IllegalStateException exception) {
            android.util.Log.w("OpenSekai", "Could not request surface refresh rate", exception);
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        getWindow().getDecorView().post(this::applyFrameRate);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) applyFrameRate();
    }

    @Override
    protected void onDestroy() {
        getWindow().getDecorView().getViewTreeObserver().removeOnGlobalLayoutListener(surfaceFinder);
        for (SurfaceHolder holder : renderSurfaces) holder.removeCallback(surfaceCallback);
        renderSurfaces.clear();
        super.onDestroy();
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (!ShareExportHelper.OnActivityResult(requestCode, resultCode, data)) {
            super.onActivityResult(requestCode, resultCode, data);
        }
    }
}
