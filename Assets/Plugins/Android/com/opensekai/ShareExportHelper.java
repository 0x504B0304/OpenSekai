package com.opensekai;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.provider.MediaStore;
import android.provider.OpenableColumns;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

public class ShareExportHelper {
    public static void ShareVideo(String contentUri) {
        Uri uri = Uri.parse(contentUri);
        if (!"content".equals(uri.getScheme())) throw new IllegalArgumentException("Invalid saved video URI");
        Activity activity = UnityPlayer.currentActivity;
        Intent intent = new Intent(Intent.ACTION_SEND);
        intent.setType("video/mp4");
        intent.putExtra(Intent.EXTRA_STREAM, uri);
        intent.setClipData(android.content.ClipData.newRawUri("OpenSekai video", uri));
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(Intent.createChooser(intent, "分享视频"));
    }
    private static final int REQUEST_CODE_SAVE = 2001;
    private static final int REQUEST_CODE_OPEN = 2002;
    private static String pendingSourcePath;
    private static String pendingCallbackObject;
    private static String pendingCallbackMethod;
    private static String pendingOpenCallbackObject;
    private static String pendingOpenCallbackMethod;

    public static void SaveAndShare(String sourcePath, String callbackObject, String callbackMethod) {
        pendingSourcePath = sourcePath;
        pendingCallbackObject = callbackObject;
        pendingCallbackMethod = callbackMethod;

        Activity activity = UnityPlayer.currentActivity;
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("application/zip");
        intent.putExtra(Intent.EXTRA_TITLE, new File(sourcePath).getName());
        activity.startActivityForResult(intent, REQUEST_CODE_SAVE);
    }

    public static void OpenFile(String callbackObject, String callbackMethod) {
        pendingOpenCallbackObject = callbackObject;
        pendingOpenCallbackMethod = callbackMethod;

        Activity activity = UnityPlayer.currentActivity;
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("application/zip");
        activity.startActivityForResult(intent, REQUEST_CODE_OPEN);
    }

    public static boolean OnActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode == REQUEST_CODE_SAVE) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                Uri uri = data.getData();
                if (uri != null && pendingSourcePath != null) {
                    try {
                        copyFileToUri(pendingSourcePath, uri);
                        UnityPlayer.UnitySendMessage(pendingCallbackObject, pendingCallbackMethod, "success");
                        clearPending();
                        return true;
                    } catch (Exception e) {
                        UnityPlayer.UnitySendMessage(pendingCallbackObject, pendingCallbackMethod, "error:" + e.getMessage());
                        clearPending();
                        return true;
                    }
                }
            } else {
                UnityPlayer.UnitySendMessage(pendingCallbackObject, pendingCallbackMethod, "cancelled");
                clearPending();
                return true;
            }
        } else if (requestCode == REQUEST_CODE_OPEN) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                Uri uri = data.getData();
                if (uri != null && pendingOpenCallbackObject != null && pendingOpenCallbackMethod != null) {
                    try {
                        String destPath = copyFileFromUri(uri);
                        UnityPlayer.UnitySendMessage(pendingOpenCallbackObject, pendingOpenCallbackMethod, "success:" + destPath);
                        clearPendingOpen();
                        return true;
                    } catch (Exception e) {
                        UnityPlayer.UnitySendMessage(pendingOpenCallbackObject, pendingOpenCallbackMethod, "error:" + e.getMessage());
                        clearPendingOpen();
                        return true;
                    }
                }
            } else {
                UnityPlayer.UnitySendMessage(pendingOpenCallbackObject, pendingOpenCallbackMethod, "cancelled");
                clearPendingOpen();
                return true;
            }
        }
        return false;
    }

    private static void clearPending() {
        pendingSourcePath = null;
        pendingCallbackObject = null;
        pendingCallbackMethod = null;
    }

    private static void clearPendingOpen() {
        pendingOpenCallbackObject = null;
        pendingOpenCallbackMethod = null;
    }

    private static String copyFileFromUri(Uri sourceUri) throws Exception {
        Activity activity = UnityPlayer.currentActivity;
        ContentResolver resolver = activity.getContentResolver();

        // Get file name from URI
        String fileName = null;
        try (android.database.Cursor cursor = resolver.query(sourceUri, null, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                int nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (nameIndex >= 0) {
                    fileName = cursor.getString(nameIndex);
                }
            }
        }
        if (fileName == null) {
            fileName = "restored_backup.zip";
        }

        // Create temp file in cache directory
        File cacheDir = activity.getExternalFilesDir(null);
        if (cacheDir == null) {
            cacheDir = activity.getCacheDir();
        }
        File destFile = new File(cacheDir, fileName);

        // Copy content to temp file
        try (InputStream inputStream = resolver.openInputStream(sourceUri);
             FileOutputStream outputStream = new FileOutputStream(destFile)) {
            if (inputStream == null) {
                throw new Exception("Failed to open input stream for URI");
            }
            byte[] buffer = new byte[8192];
            int length;
            while ((length = inputStream.read(buffer)) > 0) {
                outputStream.write(buffer, 0, length);
            }
        }

        return destFile.getAbsolutePath();
    }

    private static void copyFileToUri(String sourcePath, Uri destUri) throws Exception {
        File sourceFile = new File(sourcePath);
        Activity activity = UnityPlayer.currentActivity;
        OutputStream outputStream = activity.getContentResolver().openOutputStream(destUri);
        if (outputStream == null) {
            throw new Exception("Failed to open output stream");
        }
        FileInputStream inputStream = new FileInputStream(sourceFile);
        byte[] buffer = new byte[8192];
        int length;
        while ((length = inputStream.read(buffer)) > 0) {
            outputStream.write(buffer, 0, length);
        }
        inputStream.close();
        outputStream.close();
    }

    public static void ShareFile(String sourcePath) {
        Activity activity = UnityPlayer.currentActivity;
        File sourceFile = new File(sourcePath);
        try {
            String fileName = sourceFile.getName();

            // Use MediaStore to create a shareable URI
            ContentValues values = new ContentValues();
            values.put(MediaStore.Downloads.DISPLAY_NAME, fileName);
            values.put(MediaStore.Downloads.MIME_TYPE, "application/zip");
            values.put(MediaStore.Downloads.IS_PENDING, 1);

            Uri collection = MediaStore.Downloads.getContentUri(MediaStore.VOLUME_EXTERNAL_PRIMARY);
            Uri destUri = activity.getContentResolver().insert(collection, values);

            if (destUri == null) {
                // Fallback: try cache dir with file provider approach
                ShareFileViaCache(sourceFile, activity);
                return;
            }

            // Copy file to MediaStore
            OutputStream outputStream = activity.getContentResolver().openOutputStream(destUri);
            if (outputStream == null) {
                activity.getContentResolver().delete(destUri, null, null);
                ShareFileViaCache(sourceFile, activity);
                return;
            }

            FileInputStream inputStream = new FileInputStream(sourceFile);
            byte[] buffer = new byte[8192];
            int length;
            while ((length = inputStream.read(buffer)) > 0) {
                outputStream.write(buffer, 0, length);
            }
            inputStream.close();
            outputStream.close();

            // Clear pending flag
            values.clear();
            values.put(MediaStore.Downloads.IS_PENDING, 0);
            activity.getContentResolver().update(destUri, values, null, null);

            // Share the file
            Intent shareIntent = new Intent(Intent.ACTION_SEND);
            shareIntent.setType("application/zip");
            shareIntent.putExtra(Intent.EXTRA_STREAM, destUri);
            shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.startActivity(Intent.createChooser(shareIntent, "分享谱面"));
        } catch (Exception e) {
            e.printStackTrace();
            // Fallback: try sharing via cache file
            try {
                ShareFileViaCache(sourceFile, activity);
            } catch (Exception ex) {
                ex.printStackTrace();
            }
        }
    }

    private static void ShareFileViaCache(File sourceFile, Activity activity) throws Exception {
        File cacheDir = activity.getExternalFilesDir(null);
        if (cacheDir == null) {
            cacheDir = activity.getCacheDir();
        }
        File shareFile = new File(cacheDir, sourceFile.getName());

        // Copy to cache
        FileInputStream input = new FileInputStream(sourceFile);
        OutputStream output = new FileOutputStream(shareFile);
        byte[] buffer = new byte[8192];
        int length;
        while ((length = input.read(buffer)) > 0) {
            output.write(buffer, 0, length);
        }
        input.close();
        output.close();

        shareFile.setReadable(true, false);

        Uri shareUri = Uri.fromFile(shareFile);
        Intent shareIntent = new Intent(Intent.ACTION_SEND);
        shareIntent.setType("application/zip");
        shareIntent.putExtra(Intent.EXTRA_STREAM, shareUri);
        shareIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(Intent.createChooser(shareIntent, "分享谱面"));
    }

}
