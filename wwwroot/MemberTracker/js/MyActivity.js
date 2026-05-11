let activityCache = [];
const activityImageFallback = '/images/hero-runner.jpg';

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('sortSelect')?.addEventListener('change', loadActivities);
    document.getElementById('dateFilterSelect')?.addEventListener('change', loadActivities);
    loadActivities();
});

async function loadActivities() {
    const wrapper = document.querySelector('[data-activities-url]');
    const url = new URL(wrapper?.dataset.activitiesUrl || '/AccountProfile/MemberActivities', window.location.origin);
    url.searchParams.set('sort', document.getElementById('sortSelect')?.value || 'createdAt_desc');
    url.searchParams.set('dateFilter', document.getElementById('dateFilterSelect')?.value || 'all');

    const grid = document.getElementById('activityGrid');
    const status = document.getElementById('activityStatus');
    grid.setAttribute('aria-busy', 'true');

    try {
        const response = await fetch(url);
        const result = await response.json();
        activityCache = result.activities || [];
        renderActivities(activityCache);
    } catch {
        status.textContent = 'Unable to load activities.';
        status.style.display = 'block';
    } finally {
        grid.setAttribute('aria-busy', 'false');
    }
}

function renderActivities(activities) {
    const grid = document.getElementById('activityGrid');
    const status = document.getElementById('activityStatus');
    grid.innerHTML = '';

    if (!activities.length) {
        status.textContent = 'No activities found.';
        status.style.display = 'block';
        return;
    }

    status.style.display = 'none';
    activities.forEach(activity => {
        const card = document.createElement('button');
        card.type = 'button';
        card.className = 'activity-card';
        card.onclick = () => openActivityModal(activity.uploadId);
        const proofUrl = activity.proofUrl || activityImageFallback;
        card.innerHTML = `
            <img class="activity-card__image" src="${escapeAttribute(proofUrl)}" alt="${escapeAttribute(activity.activityName)} proof" onerror="this.onerror=null; this.src='${activityImageFallback}'">
            <div class="activity-card__body">
                <h3 class="activity-card__title">${escapeHtml(activity.activityName)}</h3>
                <div class="activity-card__meta">
                    <span>${formatDate(activity.activityDate)}</span>
                    <strong>${Number(activity.distanceKm).toFixed(2)} km</strong>
                </div>
            </div>`;
        grid.appendChild(card);
    });
}

function openActivityModal(uploadId) {
    const activity = activityCache.find(item => item.uploadId === uploadId);
    if (!activity) return;

    const modalImage = document.getElementById('modal-activity-img');
    if (modalImage) {
        modalImage.onerror = () => {
            modalImage.onerror = null;
            modalImage.src = activityImageFallback;
        };
        modalImage.src = activity.proofUrl || activityImageFallback;
    }
    document.getElementById('modal-activity-title').textContent = activity.title || activity.activityName;
    document.getElementById('modal-activity-type').textContent = activity.activityName;
    document.getElementById('modal-activity-distance').textContent = `${Number(activity.distanceKm).toFixed(2)} km`;
    document.getElementById('modal-activity-pace').textContent = formatPace(activity.avgPaceSecPerKm);
    document.getElementById('modal-activity-time').textContent = formatDuration(activity.movingTimeSec);
    document.getElementById('modal-activity-steps').textContent = Number(activity.steps || 0).toLocaleString();
    document.getElementById('modal-activity-date').textContent = formatDate(activity.activityDate);
    document.getElementById('activity-modal').classList.add('is-open');
}

function closeActivityModal() {
    document.getElementById('activity-modal')?.classList.remove('is-open');
}

function formatDuration(totalSeconds) {
    const seconds = Number(totalSeconds || 0);
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

function formatPace(secondsPerKm) {
    const seconds = Number(secondsPerKm || 0);
    if (seconds <= 0) return '-';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${String(s).padStart(2, '0')} / km`;
}

function formatDate(value) {
    if (!value) return '-';
    return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function escapeHtml(value) {
    return String(value || '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function escapeAttribute(value) {
    return escapeHtml(value).replace(/`/g, '&#096;');
}
