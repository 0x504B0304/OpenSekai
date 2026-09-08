package com.opensekai;

import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Context;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.io.OutputStream;

/** Writes app-owned media without read-library permission; publishes only after a complete copy. */
public final class VideoGalleryJob {
    private volatile boolean done, cancelled;
    private volatile float progress;
    private volatile String uri = "", error = "";

    public VideoGalleryJob(String sourcePath, String filename, String albumName) {
        Context context = UnityPlayer.currentActivity.getApplicationContext();
        new Thread(() -> {
            try { uri = save(context, sourcePath, filename, albumName); }
            catch (Exception e) { error = e.getMessage() == null ? e.getClass().getSimpleName() : e.getMessage(); }
            finally { done = true; }
        }, "OpenSekai gallery export").start();
    }

    public boolean isDone() { return done; }
    public float getProgress() { return progress; }
    public String getUri() { return uri; }
    public String getError() { return error; }
    public void cancel() { cancelled = true; }

    private String save(Context context, String sourcePath, String filename, String albumName) throws Exception {
        File source = new File(sourcePath);
        if (!source.isFile() || source.length() == 0) throw new IOException("视频文件不存在或为空。");
        ContentResolver resolver = context.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.Video.Media.DISPLAY_NAME, filename);
        values.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");
        File legacyFile = null;
        if (Build.VERSION.SDK_INT >= 29) {
            values.put(MediaStore.Video.Media.RELATIVE_PATH, Environment.DIRECTORY_MOVIES + "/" + albumName);
            values.put(MediaStore.Video.Media.IS_PENDING, 1);
        } else {
            File directory = new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_MOVIES), albumName);
            if (!directory.isDirectory() && !directory.mkdirs()) throw new IOException("无法创建相册目录，请检查存储权限。");
            // Reserve a unique path without overwriting existing videos.
            legacyFile = new File(directory, filename);
            for (int i = 1; !legacyFile.createNewFile(); i++)
                legacyFile = new File(directory, filename.replaceFirst("(?i)\\.mp4$", "") + "_" + i + ".mp4");
            values.put(MediaStore.Video.Media.DATA, legacyFile.getAbsolutePath());
        }
        Uri result = null;
        try {
            result = resolver.insert(MediaStore.Video.Media.EXTERNAL_CONTENT_URI, values);
            if (result == null) throw new IOException("无法创建相册视频记录。");
            try (FileInputStream input = new FileInputStream(source); OutputStream output = resolver.openOutputStream(result, "w")) {
                if (output == null) throw new IOException("无法写入相册。");
                byte[] buffer = new byte[65536];
                long copied = 0; int count;
                while ((count = input.read(buffer)) != -1) {
                    if (cancelled) throw new IOException("保存已取消，原视频仍然保留。");
                    output.write(buffer, 0, count); copied += count;
                    progress = copied / (float)source.length();
                }
                output.flush();
                if (copied != source.length()) throw new IOException("视频复制不完整。");
            }
            if (cancelled) throw new IOException("保存已取消，原视频仍然保留。");
            if (Build.VERSION.SDK_INT >= 29) {
                ContentValues published = new ContentValues();
                published.put(MediaStore.Video.Media.IS_PENDING, 0);
                if (resolver.update(result, published, null, null) != 1) throw new IOException("无法发布相册视频。");
            }
            progress = 1;
            return result.toString();
        } catch (Exception e) {
            if (result != null) try { resolver.delete(result, null, null); } catch (Exception ignored) { }
            if (legacyFile != null) legacyFile.delete();
            throw e;
        }
    }
}
