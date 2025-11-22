// SignalR connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/amplifierHub")
    .withAutomaticReconnect()
    .build();

// DOM elements
const elements = {
    statusLabel: document.getElementById('statusLabel'),
    bandLabel: document.getElementById('bandLabel'),
    pwrBar: document.getElementById('pwrBar'),
    pwrBarRed: document.getElementById('pwrBarRed'),
    pwrLabel: document.getElementById('pwrLabel'),
    driveLabel: document.getElementById('driveLabel'),
    reflBar: document.getElementById('reflBar'),
    reflBarRed: document.getElementById('reflBarRed'),
    reflLabel: document.getElementById('reflLabel'),
    tempBar: document.getElementById('tempBar'),
    tempLabel: document.getElementById('tempLabel'),
    tempRow: document.getElementById('tempRow'),
    swrLabel: document.getElementById('swrLabel'),
    effLabel: document.getElementById('effLabel'),
    gainLabel: document.getElementById('gainLabel'),
    fanLabel: document.getElementById('fanLabel'),
    errorBanner: document.getElementById('errorBanner'),
    errorText: document.getElementById('errorText'),
    controller: document.querySelector('.acom-controller')
};

// Update UI with telemetry data
function updateTelemetry(telemetry) {
    // Connection status
    if (!telemetry.isConnected) {
        elements.controller.classList.add('disconnected');
        elements.statusLabel.textContent = '--';
        elements.bandLabel.textContent = '--';
        elements.pwrLabel.textContent = '--W';
        elements.driveLabel.textContent = '--W';
        elements.reflLabel.textContent = '--R';
        elements.tempLabel.textContent = '--°C';
        return;
    }

    elements.controller.classList.remove('disconnected');

    // Status
    elements.statusLabel.textContent = telemetry.statusText;
    elements.statusLabel.className = 'status-text ' + telemetry.statusColor;

    // Band
    elements.bandLabel.textContent = telemetry.bandName;

    // Output power
    const nominalPower = modelConfig.nominalForwardPower;
    const maxPower = modelConfig.maxForwardPower;
    const normalPercent = Math.min(100, (telemetry.outputPower / nominalPower) * 100);
    const warningPercent = telemetry.outputPower > nominalPower
        ? Math.min(100, ((telemetry.outputPower - nominalPower) / (maxPower - nominalPower)) * 100)
        : 0;

    elements.pwrBar.style.width = normalPercent + '%';
    elements.pwrBarRed.style.width = warningPercent + '%';
    elements.pwrLabel.textContent = Math.round(telemetry.outputPower) + 'W';

    // Drive power
    elements.driveLabel.textContent = telemetry.drivePower.toFixed(1) + 'W';

    // Reflected power
    const nominalReverse = modelConfig.nominalReversePower;
    const maxReverse = modelConfig.maxReversePower;
    const reflNormalPercent = Math.min(100, (telemetry.reflectedPower / nominalReverse) * 100);
    const reflWarningPercent = telemetry.reflectedPower > nominalReverse
        ? Math.min(100, ((telemetry.reflectedPower - nominalReverse) / (maxReverse - nominalReverse)) * 100)
        : 0;

    elements.reflBar.style.width = reflNormalPercent + '%';
    elements.reflBarRed.style.width = reflWarningPercent + '%';
    elements.reflLabel.textContent = Math.round(telemetry.reflectedPower) + 'R';

    // Temperature
    if (modelConfig.showTemperature) {
        elements.tempRow.style.display = 'flex';
        const tempPercent = Math.min(100, telemetry.temperature);
        elements.tempBar.style.width = tempPercent + '%';
        elements.tempBar.className = telemetry.temperatureWarning
            ? 'progress-fill temp warning'
            : 'progress-fill temp';
        elements.tempLabel.textContent = telemetry.temperature + '°C';
        elements.tempLabel.style.color = telemetry.temperatureWarning ? '#dc143c' : '';
    } else {
        elements.tempRow.style.display = 'none';
    }

    // Optional metrics
    if (settings.showSwr && telemetry.swr > 0 && telemetry.outputPower > 0) {
        elements.swrLabel.textContent = 'SWR: ' + telemetry.swr.toFixed(1);
    } else {
        elements.swrLabel.textContent = '';
    }

    if (telemetry.showEfficiency && telemetry.efficiency > 0) {
        elements.effLabel.textContent = 'Eff: ' + telemetry.efficiency.toFixed(0) + '%';
    } else {
        elements.effLabel.textContent = '';
    }

    if (telemetry.showGain && telemetry.gain > 0) {
        elements.gainLabel.textContent = 'Gain: ' + telemetry.gain.toFixed(1) + 'dB';
    } else {
        elements.gainLabel.textContent = '';
    }

    elements.fanLabel.textContent = telemetry.fanText;

    // Error banner
    if (telemetry.hasError) {
        elements.errorBanner.style.display = 'block';
        elements.errorText.textContent = telemetry.errorMessage;
    } else {
        elements.errorBanner.style.display = 'none';
    }
}

// Button handlers
document.getElementById('standbyBtn').addEventListener('click', async () => {
    await connection.invoke('SendStandby');
});

document.getElementById('operateBtn').addEventListener('click', async () => {
    await connection.invoke('SendOperate');
});

document.getElementById('offBtn').addEventListener('click', async () => {
    await connection.invoke('SendOff');
});

// Right-click standby button for settings
document.getElementById('standbyBtn').addEventListener('contextmenu', (e) => {
    e.preventDefault();
    document.getElementById('settingsModal').style.display = 'flex';
});

// Error banner click to dismiss
document.getElementById('errorBanner').addEventListener('click', async () => {
    await connection.invoke('SendOperate');
});

// Settings modal
document.getElementById('saveSettings').addEventListener('click', async () => {
    const newSettings = {
        comPort: document.getElementById('portSelect').value,
        amplifierModel: document.getElementById('modelSelect').value,
        showEfficiency: document.getElementById('showEfficiency').checked,
        showGain: document.getElementById('showGain').checked,
        showSwr: document.getElementById('showSwr').checked
    };

    await fetch('/Home/UpdateSettings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newSettings)
    });

    // Update local settings
    Object.assign(settings, newSettings);

    document.getElementById('settingsModal').style.display = 'none';
    location.reload(); // Reload to get new model config
});

document.getElementById('cancelSettings').addEventListener('click', () => {
    document.getElementById('settingsModal').style.display = 'none';
});

// SignalR event handler
connection.on('TelemetryUpdate', updateTelemetry);

// Start connection
connection.start()
    .then(() => console.log('SignalR connected'))
    .catch(err => console.error('SignalR connection error:', err));
