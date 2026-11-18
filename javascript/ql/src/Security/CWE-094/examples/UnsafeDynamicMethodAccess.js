// API methods
function play(data) {
  // ...
}
function pause(data) {
  // ...
}

window.addEventListener("message", (ev) => {
    // Only accept messages from the expected trusted origin.
    if (ev.origin !== "https://trusted.example.com") {
        return;
    }

    let message = JSON.parse(ev.data);

    // Let the parent frame call the 'play' or 'pause' function
    window[message.name](message.payload);
});
