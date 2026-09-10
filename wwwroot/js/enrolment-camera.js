document.addEventListener('DOMContentLoaded', () => {
    const input = document.querySelector('input[type="file"][name="PhotoFile"]');
    if (!input) return;
    const panel = document.createElement('div');
    panel.className = 'mt-3';
    panel.innerHTML = '<button type="button" class="btn btn-outline-secondary"><i class="bi bi-camera" aria-hidden="true"></i> Open camera</button><video class="d-none mt-2" autoplay muted playsinline style="width:100%;max-width:480px;aspect-ratio:4/3;object-fit:cover"></video><button type="button" class="btn btn-success d-none mt-2">Capture photo</button><button type="button" class="btn btn-outline-secondary d-none mt-2 ms-2">Close camera</button><p class="small mt-2" role="status" aria-live="polite"></p>';
    input.after(panel);
    const [open, capture, close] = panel.querySelectorAll('button');
    const video = panel.querySelector('video');
    const status = panel.querySelector('[role="status"]');
    let stream;
    function stop() {
        stream?.getTracks().forEach(track => track.stop());
        stream = null;
        video.srcObject = null;
        [video, capture, close].forEach(element => element.classList.add('d-none'));
        open.disabled = false;
    }
    open.addEventListener('click', async () => {
        open.disabled = true;
        status.textContent = '';
        try {
            if (!navigator.mediaDevices?.getUserMedia) throw new Error('unavailable');
            stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false });
            video.srcObject = stream;
            [video, capture, close].forEach(element => element.classList.remove('d-none'));
            await video.play();
        } catch {
            stop();
            status.textContent = 'Camera unavailable. Allow camera access on HTTPS or choose a photo file.';
        }
    });
    capture.addEventListener('click', () => {
        if (!video.videoWidth) return;
        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0);
        canvas.toBlob(blob => {
            if (!blob) return;
            const transfer = new DataTransfer();
            transfer.items.add(new File([blob], 'enrollee-photo.jpg', { type: 'image/jpeg' }));
            input.files = transfer.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
            status.textContent = 'Photo captured.';
            stop();
        }, 'image/jpeg', 0.9);
    });
    close.addEventListener('click', stop);
    input.form?.addEventListener('submit', stop);
    window.addEventListener('pagehide', stop);
});
