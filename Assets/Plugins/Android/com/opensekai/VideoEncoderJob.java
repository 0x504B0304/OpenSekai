package com.opensekai;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaCodecList;
import android.media.MediaExtractor;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.os.SystemClock;
import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.nio.ByteBuffer;
import java.util.Locale;
import java.util.UUID;

/** Owns all codecs on one worker thread. Unity only polls immutable/volatile status. */
public final class VideoEncoderJob {
    private volatile boolean cancelled, done, succeeded;
    private volatile float progress;
    private volatile String status = "准备编码…", error = "";
    private final String frames, audio, destination;
    private final int width, height, fps, frameCount, videoBitrate, audioBitrate;

    public VideoEncoderJob(String frames, String audio, String destination, int width, int height,
                           int fps, int frameCount, int videoBitrate, int audioBitrate) {
        this.frames = frames; this.audio = audio; this.destination = destination;
        this.width = width; this.height = height; this.fps = fps; this.frameCount = frameCount;
        this.videoBitrate = videoBitrate; this.audioBitrate = audioBitrate;
        new Thread(this::run, "OpenSekai video export").start();
    }

    public boolean isDone() { return done; }
    public boolean isSucceeded() { return succeeded; }
    public float getProgress() { return progress; }
    public String getStatus() { return status; }
    public String getError() { return error; }
    public void cancel() { cancelled = true; }

    public static double getPcmDuration(String path) throws IOException {
        try (PcmWave wave = new PcmWave(path)) {
            return wave.remaining / (double)(wave.sampleRate * wave.channels * 2);
        }
    }

    public static String checkSupport(int width, int height, int fps) {
        try {
            MediaFormat video = videoFormat(width, height, fps, 8000000);
            if (new MediaCodecList(MediaCodecList.REGULAR_CODECS).findEncoderForFormat(video) == null)
                return "设备不支持此分辨率的 H.264/YUV420 编码。";
            if (new MediaCodecList(MediaCodecList.REGULAR_CODECS).findEncoderForFormat(audioFormat(48000, 2, 192000)) == null)
                return "设备不支持 AAC 音频编码。";
            return "";
        } catch (Exception e) { return message(e); }
    }

    private static MediaFormat videoFormat(int width, int height, int fps, int bitrate) {
        MediaFormat format = MediaFormat.createVideoFormat("video/avc", width, height);
        format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
        format.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
        format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
        return format;
    }

    private static MediaFormat audioFormat(int sampleRate, int channels, int bitrate) {
        MediaFormat format = MediaFormat.createAudioFormat("audio/mp4a-latm", sampleRate, channels);
        format.setInteger(MediaFormat.KEY_AAC_PROFILE, MediaCodecInfo.CodecProfileLevel.AACObjectLC);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate);
        format.setInteger(MediaFormat.KEY_MAX_INPUT_SIZE, 16384);
        return format;
    }

    private void run() {
        File output = new File(destination);
        String prefix = destination + "." + UUID.randomUUID();
        File video = new File(prefix + ".video.mp4"), sound = new File(prefix + ".audio.mp4"), partial = new File(prefix + ".partial.mp4");
        try {
            if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0 || fps <= 0 || frameCount <= 0)
                throw new IOException("录制尺寸、帧率或帧数无效。");
            if (output.exists()) throw new IOException("输出文件已存在，未覆盖原文件。");
            File parent = output.getParentFile();
            if (parent != null && !parent.isDirectory() && !parent.mkdirs()) throw new IOException("无法创建输出目录。");
            // Validate the WAV before spending time encoding a whole chart.
            try (PcmWave ignored = new PcmWave(audio)) { }
            encodeVideo(video);
            encodeAudio(sound);
            mux(video, sound, partial);
            checkCancelled();
            if (partial.length() == 0 || !partial.renameTo(output)) throw new IOException("无法完成视频文件写入。");
            progress = 1; status = "编码完成"; succeeded = true;
        } catch (Exception e) {
            error = cancelled ? "视频生成已取消，录制素材已保留。" : message(e);
            status = error;
        } finally {
            // Only files created by this job; never remove the recording or an existing export.
            video.delete(); sound.delete(); partial.delete();
            done = true;
        }
    }

    private void checkCancelled() throws IOException {
        if (cancelled) throw new IOException("已取消");
    }

    private static String message(Exception e) {
        return e.getMessage() == null ? e.getClass().getSimpleName() : e.getMessage();
    }

    private void encodeVideo(File file) throws Exception {
        MediaFormat format = videoFormat(width, height, fps, videoBitrate);
        try (TrackEncoder encoder = new TrackEncoder(format, file)) {
            int next = 0;
            int[] pixels = new int[width * height];
            boolean inputEnded = false;
            long lastActivity = SystemClock.elapsedRealtime();
            while (!encoder.ended) {
                checkCancelled();
                boolean moved = false;
                if (!inputEnded) {
                    int input = encoder.codec.dequeueInputBuffer(10000);
                    if (input >= 0) {
                        if (next == frameCount) {
                            encoder.codec.queueInputBuffer(input, 0, 0, next * 1000000L / fps, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                            inputEnded = true;
                        } else {
                            File frame = new File(frames, String.format(Locale.ROOT, "frame_%06d.jpg", next + 1));
                            Bitmap bitmap = BitmapFactory.decodeFile(frame.getAbsolutePath());
                            if (bitmap == null) throw new IOException("无法读取画面：" + frame.getName());
                            try {
                                if (bitmap.getWidth() != width || bitmap.getHeight() != height) throw new IOException("录制帧尺寸不一致。");
                                bitmap.getPixels(pixels, 0, width, 0, 0, width, height);
                            } finally { bitmap.recycle(); }
                            Image image = encoder.codec.getInputImage(input);
                            if (image == null) throw new IOException("此设备的编码器不提供 YUV420 输入图像。");
                            try { fillYuv(image, pixels, width, height); } finally { image.close(); }
                            encoder.codec.queueInputBuffer(input, 0, width * height * 3 / 2, next * 1000000L / fps, 0);
                            next++;
                            progress = 0.75f * next / frameCount;
                            status = "编码画面 " + next + "/" + frameCount;
                        }
                        moved = true;
                    }
                }
                moved |= encoder.drain();
                if (moved) lastActivity = SystemClock.elapsedRealtime();
                else if (SystemClock.elapsedRealtime() - lastActivity > 30000) throw new IOException("视频编码器响应超时。");
            }
            encoder.finish();
        }
    }

    private static void fillYuv(Image image, int[] pixels, int width, int height) throws IOException {
        Image.Plane[] planes = image.getPlanes();
        if (planes.length != 3) throw new IOException("编码器返回了无效的 YUV 图像。");
        ByteBuffer[] buffers = new ByteBuffer[3];
        int[] rows = new int[3], strides = new int[3];
        for (int i = 0; i < 3; i++) {
            buffers[i] = planes[i].getBuffer(); rows[i] = planes[i].getRowStride(); strides[i] = planes[i].getPixelStride();
        }
        writeYuv420(pixels, width, height, buffers, rows, strides);
    }

    static void writeYuv420(int[] pixels, int width, int height, ByteBuffer[] planes, int[] rows, int[] strides) {
        for (int y = 0; y < height; y += 2) {
            for (int x = 0; x < width; x += 2) {
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) {
                    int p = pixels[(y + dy) * width + x + dx];
                    int red = (p >> 16) & 255, green = (p >> 8) & 255, blue = p & 255;
                    put(planes[0], rows[0], strides[0], x + dx, y + dy, ((66 * red + 129 * green + 25 * blue + 128) >> 8) + 16);
                    r += red; g += green; b += blue;
                }
                r /= 4; g /= 4; b /= 4;
                put(planes[1], rows[1], strides[1], x / 2, y / 2, ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128);
                put(planes[2], rows[2], strides[2], x / 2, y / 2, ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128);
            }
        }
    }

    private static void put(ByteBuffer buffer, int rowStride, int pixelStride, int x, int y, int value) {
        buffer.put(buffer.position() + y * rowStride + x * pixelStride, (byte)Math.max(0, Math.min(255, value)));
    }

    private void encodeAudio(File file) throws Exception {
        try (PcmWave wave = new PcmWave(audio);
             TrackEncoder encoder = new TrackEncoder(audioFormat(wave.sampleRate, wave.channels, audioBitrate), file)) {
            int alignment = wave.channels * 2;
            long totalSamples = (long)Math.ceil(frameCount * (double)wave.sampleRate / fps);
            long supplied = 0;
            boolean inputEnded = false;
            byte[] block = new byte[16384];
            long lastActivity = SystemClock.elapsedRealtime();
            while (!encoder.ended) {
                checkCancelled();
                boolean moved = false;
                if (!inputEnded) {
                    int input = encoder.codec.dequeueInputBuffer(10000);
                    if (input >= 0) {
                        long timeUs = supplied * 1000000L / wave.sampleRate;
                        if (supplied == totalSamples) {
                            encoder.codec.queueInputBuffer(input, 0, 0, timeUs, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                            inputEnded = true;
                        } else {
                            ByteBuffer buffer = encoder.codec.getInputBuffer(input);
                            if (buffer == null) throw new IOException("音频编码器输入缓冲区不可用。");
                            buffer.clear();
                            int count = (int)Math.min(totalSamples - supplied, Math.min(buffer.remaining(), block.length) / alignment) * alignment;
                            if (count == 0) throw new IOException("音频编码器输入缓冲区过小。");
                            wave.readPadded(block, count);
                            buffer.put(block, 0, count);
                            encoder.codec.queueInputBuffer(input, 0, count, timeUs, 0);
                            supplied += count / alignment;
                            progress = 0.75f + 0.2f * supplied / totalSamples;
                            status = "编码音乐与音效…";
                        }
                        moved = true;
                    }
                }
                moved |= encoder.drain();
                if (moved) lastActivity = SystemClock.elapsedRealtime();
                else if (SystemClock.elapsedRealtime() - lastActivity > 30000) throw new IOException("音频编码器响应超时。");
            }
            encoder.finish();
        }
    }

    /** One elementary track per temporary MP4: add the actual codec output format before starting. */
    private static final class TrackEncoder implements AutoCloseable {
        MediaCodec codec;
        MediaMuxer muxer;
        final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
        boolean started, ended;
        int track = -1, samples;

        TrackEncoder(MediaFormat format, File file) throws Exception {
            try {
                String name = new MediaCodecList(MediaCodecList.REGULAR_CODECS).findEncoderForFormat(format);
                if (name == null) throw new IOException("设备不支持请求的音视频编码格式。");
                codec = MediaCodec.createByCodecName(name);
                codec.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
                codec.start();
                muxer = new MediaMuxer(file.getAbsolutePath(), MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            } catch (Exception e) { close(); throw e; }
        }

        boolean drain() throws Exception {
            boolean moved = false;
            // Bound each drain pass so the input producer and cancellation get a turn.
            for (int i = 0; i < 32; i++) {
                int index = codec.dequeueOutputBuffer(info, 0);
                if (index == MediaCodec.INFO_TRY_AGAIN_LATER) return moved;
                moved = true;
                if (index == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                    if (started) throw new IOException("编码过程中输出格式重复改变。");
                    track = muxer.addTrack(codec.getOutputFormat());
                    muxer.start(); started = true;
                } else if (index >= 0) {
                    try {
                        if (info.size > 0 && (info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) == 0) {
                            if (!started) throw new IOException("编码器未提供输出格式。");
                            ByteBuffer data = codec.getOutputBuffer(index);
                            if (data == null) throw new IOException("编码器输出缓冲区不可用。");
                            data.position(info.offset); data.limit(info.offset + info.size);
                            muxer.writeSampleData(track, data, info); samples++;
                        }
                        ended = (info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0;
                    } finally { codec.releaseOutputBuffer(index, false); }
                    if (ended) return true;
                }
            }
            return moved;
        }

        void finish() throws Exception {
            if (!ended || samples == 0 || !started) throw new IOException("编码轨道不完整。");
            muxer.stop(); started = false;
        }

        public void close() {
            if (codec != null) {
                try { codec.stop(); } catch (Exception ignored) { }
                try { codec.release(); } catch (Exception ignored) { }
                codec = null;
            }
            if (muxer != null) {
                if (started) try { muxer.stop(); } catch (Exception ignored) { }
                try { muxer.release(); } catch (Exception ignored) { }
                muxer = null;
            }
        }
    }

    private void mux(File video, File sound, File output) throws Exception {
        status = "合并音视频…";
        MediaExtractor picture = new MediaExtractor(), music = new MediaExtractor();
        MediaMuxer muxer = null;
        boolean started = false;
        try {
            picture.setDataSource(video.getAbsolutePath()); music.setDataSource(sound.getAbsolutePath());
            picture.selectTrack(0); music.selectTrack(0);
            MediaFormat videoFormat = picture.getTrackFormat(0), audioFormat = music.getTrackFormat(0);
            muxer = new MediaMuxer(output.getAbsolutePath(), MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            int videoTrack = muxer.addTrack(videoFormat), audioTrack = muxer.addTrack(audioFormat);
            muxer.start(); started = true;
            int capacity = Math.max(width * height * 3 / 2, 1024 * 1024);
            if (videoFormat.containsKey(MediaFormat.KEY_MAX_INPUT_SIZE)) capacity = Math.max(capacity, videoFormat.getInteger(MediaFormat.KEY_MAX_INPUT_SIZE));
            ByteBuffer data = ByteBuffer.allocateDirect(capacity);
            MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();
            while (picture.getSampleTime() >= 0 || music.getSampleTime() >= 0) {
                checkCancelled();
                long videoTime = picture.getSampleTime(), audioTime = music.getSampleTime();
                boolean useVideo = videoTime >= 0 && (audioTime < 0 || videoTime <= audioTime);
                MediaExtractor source = useVideo ? picture : music;
                data.clear();
                int size = source.readSampleData(data, 0);
                if (size < 0) throw new IOException("无法读取已编码的数据。");
                long time = source.getSampleTime();
                info.set(0, size, time, (source.getSampleFlags() & MediaExtractor.SAMPLE_FLAG_SYNC) != 0 ? MediaCodec.BUFFER_FLAG_KEY_FRAME : 0);
                muxer.writeSampleData(useVideo ? videoTrack : audioTrack, data, info);
                source.advance();
                progress = 0.95f + 0.04f * Math.min(1f, time / (frameCount * 1000000f / fps));
            }
            muxer.stop(); started = false;
        } finally {
            picture.release(); music.release();
            if (muxer != null) {
                if (started) try { muxer.stop(); } catch (Exception ignored) { }
                muxer.release();
            }
        }
    }

    /** RIFF chunks may include metadata and padding; never assume a fixed 44-byte header. */
    static final class PcmWave implements AutoCloseable {
        final RandomAccessFile file;
        int sampleRate, channels;
        long remaining;

        PcmWave(String path) throws IOException {
            if (path == null || path.isEmpty()) throw new IOException("录制音频缺失。");
            file = new RandomAccessFile(path, "r");
            try {
                if (file.readInt() != 0x52494646) throw new IOException("不是 RIFF 音频。");
                long riffEnd = Math.min(file.length(), uint32() + 8);
                if (file.readInt() != 0x57415645) throw new IOException("不是 WAV 音频。");
                long dataOffset = -1, dataLength = 0;
                boolean validFormat = false;
                while (file.getFilePointer() + 8 <= riffEnd) {
                    int id = file.readInt(); long size = uint32(), start = file.getFilePointer();
                    if (size > riffEnd - start) throw new IOException("WAV 文件被截断。");
                    if (id == 0x666d7420) {
                        if (size < 16) throw new IOException("WAV 格式块损坏。");
                        int format = ushort(); channels = ushort(); sampleRate = (int)uint32();
                        uint32(); int align = ushort(), bits = ushort();
                        validFormat = format == 1 && bits == 16 && (channels == 1 || channels == 2)
                            && sampleRate >= 8000 && sampleRate <= 96000 && align == channels * 2;
                    } else if (id == 0x64617461) { dataOffset = start; dataLength = size; }
                    file.seek(start + size + (size & 1));
                }
                if (!validFormat || dataOffset < 0 || dataLength == 0 || dataLength % (channels * 2) != 0)
                    throw new IOException("需要完整的 16 位单声道或双声道 PCM WAV 音频。");
                remaining = dataLength; file.seek(dataOffset);
            } catch (IOException e) { file.close(); throw e; }
        }

        private int ushort() throws IOException { return Short.reverseBytes(file.readShort()) & 0xffff; }
        private long uint32() throws IOException { return Integer.reverseBytes(file.readInt()) & 0xffffffffL; }
        void readPadded(byte[] block, int count) throws IOException {
            int available = (int)Math.min(remaining, count);
            file.readFully(block, 0, available); remaining -= available;
            java.util.Arrays.fill(block, available, count, (byte)0);
        }
        public void close() throws IOException { file.close(); }
    }
}
