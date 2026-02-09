document.addEventListener('DOMContentLoaded', function() {
    checkServiceStatus();
});

async function checkServiceStatus() {
    const badge = document.getElementById('status-badge');
    const dot = badge.querySelector('.status-dot');
    const text = badge.querySelector('.status-text');

    try {
        const response = await fetch('/api/status');
        if (response.ok) {
            const data = await response.json();
            if (data.data === true) {
                setStatus(badge, dot, text, true, 'Service Running');
            } else {
                setStatus(badge, dot, text, false, 'Service Starting...');
            }
        } else {
            setStatus(badge, dot, text, false, 'Service Unavailable');
        }
    } catch (error) {
        setStatus(badge, dot, text, false, 'Connection Error');
    }
}

function setStatus(badge, dot, text, isHealthy, message) {
    text.textContent = message;
    if (isHealthy) {
        badge.style.background = 'rgba(16, 185, 129, 0.15)';
        badge.style.borderColor = 'rgba(16, 185, 129, 0.3)';
        badge.style.color = '#10b981';
        dot.style.background = '#10b981';
    } else {
        badge.style.background = 'rgba(239, 68, 68, 0.15)';
        badge.style.borderColor = 'rgba(239, 68, 68, 0.3)';
        badge.style.color = '#ef4444';
        dot.style.background = '#ef4444';
    }
}
