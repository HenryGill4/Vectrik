// machine-state-client.js - SignalR client for live machine telemetry
window.MachineStateClient = {
    connection: null,

    start: function (hubUrl, onStateUpdate) {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not loaded');
            return;
        }

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        this.connection.on('MachineStateUpdated', function (machineId, state) {
            if (typeof onStateUpdate === 'function') {
                onStateUpdate(machineId, state);
            }
        });

        this.connection.start().catch(function (err) {
            console.error('SignalR connection error:', err);
        });
    },

    stop: function () {
        if (this.connection) {
            this.connection.stop();
        }
    }
};
