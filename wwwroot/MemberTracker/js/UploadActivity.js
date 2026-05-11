let selectedFile = null;

document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('activityForm');
    if (!form) return;

    const today = new Date().toISOString().slice(0, 10);
    const activityDate = document.getElementById('activityDate');
    if (activityDate && !activityDate.value) {
        activityDate.value = today;
    }

    form.addEventListener('submit', submitActivity);
});

function openTimePicker() {
    openPicker('timePickerModal');
}

function openDistancePicker() {
    openPicker('distancePickerModal');
}

function openPicker(id) {
    const modal = document.getElementById(id);
    if (modal) {
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
    }
}

function closePicker(id) {
    const modal = document.getElementById(id);
    if (modal) {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    }
}

function applyTime() {
    const hours = clampNumber(document.getElementById('hourInput')?.value, 0, 23);
    const minutes = clampNumber(document.getElementById('minuteInput')?.value, 0, 59);
    const seconds = clampNumber(document.getElementById('secondInput')?.value, 0, 59);
    const total = (hours * 3600) + (minutes * 60) + seconds;

    document.getElementById('MovingTime').value = total;
    document.getElementById('movingTimeDisplay').value = `${pad2(hours)}:${pad2(minutes)}:${pad2(seconds)}`;
    closePicker('timePickerModal');
}

function applyDistance() {
    const whole = clampNumber(document.getElementById('distanceWhole')?.value, 0, 999);
    const decimalInput = document.getElementById('distanceDecimal');
    const decimal = String(decimalInput?.value || '00').replace(/\D/g, '').padEnd(2, '0').slice(0, 2);
    const unit = document.getElementById('distanceUnit')?.value || 'km';
    let distance = Number(`${whole}.${decimal}`);

    if (unit === 'mi') {
        distance *= 1.60934;
    }

    document.getElementById('DistanceKm').value = distance.toFixed(2);
    document.getElementById('DistanceUnit').value = unit;
    document.getElementById('distanceDisplay').value = `${whole}.${decimal} ${unit}`;
    closePicker('distancePickerModal');
}

function handleFileSelect(event) {
    selectedFile = event.target.files && event.target.files.length > 0 ? event.target.files[0] : null;
    const previewContainer = document.getElementById('previewContainer');
    const previewImage = document.getElementById('previewImage');
    const removeBtn = document.getElementById('removeBtn');

    if (!selectedFile) return;

    previewImage.src = URL.createObjectURL(selectedFile);
    previewContainer.classList.add('has-image');
    removeBtn.style.display = 'block';
}

function removeImage() {
    selectedFile = null;
    const input = document.getElementById('proofFile');
    const previewContainer = document.getElementById('previewContainer');
    const previewImage = document.getElementById('previewImage');
    const removeBtn = document.getElementById('removeBtn');

    if (input) input.value = '';
    if (previewImage) previewImage.removeAttribute('src');
    if (previewContainer) previewContainer.classList.remove('has-image');
    if (removeBtn) removeBtn.style.display = 'none';
}

async function submitActivity(event) {
    event.preventDefault();
    clearWarnings();

    const wrapper = document.querySelector('[data-upload-url]');
    const uploadUrl = wrapper?.dataset.uploadUrl;
    const myActivityUrl = wrapper?.dataset.myActivityUrl;
    const activityName = document.getElementById('activityName').value;
    const activityDate = document.getElementById('activityDate').value;
    const movingTime = Number(document.getElementById('MovingTime').value || 0);
    const distanceKm = Number(document.getElementById('DistanceKm').value || 0);
    const steps = Number(document.getElementById('stepsInput').value || 0);

    let valid = true;
    valid = showWarning('activityWarning', activityName ? '' : 'Select an activity.') && valid;
    valid = showWarning('dateWarning', activityDate ? '' : 'Select a date.') && valid;
    valid = showWarning('timeWarning', movingTime > 0 ? '' : 'Select moving time.') && valid;
    valid = showWarning('distanceWarning', distanceKm > 0 ? '' : 'Select distance.') && valid;
    valid = showWarning('stepsWarning', steps > 0 ? '' : 'Enter steps.') && valid;
    valid = showWarning('proofWarning', selectedFile ? '' : 'Upload a screenshot proof image.') && valid;
    if (!valid || !uploadUrl) return;

    const data = new FormData();
    data.append('ActivityName', activityName);
    data.append('ActivityDate', activityDate);
    data.append('MovingTime', String(movingTime));
    data.append('DistanceKm', String(distanceKm));
    data.append('Steps', String(steps));
    if (selectedFile) {
        data.append('ProofFile', selectedFile);
    }

    const submitBtn = document.getElementById('submitBtn');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Submitting...';

    try {
        const response = await fetch(uploadUrl, { method: 'POST', body: data });
        const result = await response.json().catch(() => ({}));
        if (!response.ok || result.success === false) {
            showWarning('proofWarning', result.message || 'Upload failed.');
            return;
        }

        window.location.href = myActivityUrl || '/AccountProfile/MyActivity';
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Submit Activity';
    }
}

function clearWarnings() {
    document.querySelectorAll('.input-warning').forEach(item => {
        item.textContent = '';
    });
}

function showWarning(id, message) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = message;
    }
    return !message;
}

function clampNumber(value, min, max) {
    const parsed = Number.parseInt(value || '0', 10);
    if (Number.isNaN(parsed)) return min;
    return Math.min(max, Math.max(min, parsed));
}

function pad2(value) {
    return String(value).padStart(2, '0');
}
