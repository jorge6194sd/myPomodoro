    // ================== GLOBAL STATE ==================
    let timerInterval = null;
    let isPaused = false;
    let isWorkSession = true;
    let workDuration = 30;
    let restDuration = 5;
    let thirtyMinSessionCount = 0;

    let endTime = 0;
    let timeRemaining = 0;

    let sessionLogs = [];
    let pendingFocusRating = 0;
    let selectedCategory = "";

    const sessionLabelEl = document.getElementById("session-label");
    const taskCategoryEl = document.getElementById("taskCategory");
    const startBtn = document.getElementById("start-btn");
    const pauseBtn = document.getElementById("pause-btn");
    const resetBtn = document.getElementById("reset-btn");
    const recordBtn = document.getElementById("record-btn");
    const saveRatingBtn = document.getElementById("saveRatingBtn");
    const stars = document.querySelectorAll("#starRating i");
    const completedSessionsEl = document.getElementById("completed-sessions");

    // On page load, fetch improvement stats & chart data
    window.onload = () => {
        fetchVolumeIncrease();
        loadDailyTotalsChart();
    };

    // Category dropdown
    taskCategoryEl.addEventListener("change", () => {
        selectedCategory = taskCategoryEl.value;
    });

    // Timer buttons
    startBtn.addEventListener("click", startTimer);
    pauseBtn.addEventListener("click", pauseTimer);
    resetBtn.addEventListener("click", resetTimer);

    // Recording
    recordBtn.addEventListener("click", () => recordSessions(false));

    // Star rating
    saveRatingBtn.addEventListener("click", saveFocusRating);
    stars.forEach(star => {
        star.addEventListener("click", () => {
            const val = parseInt(star.getAttribute("data-value"));
            pendingFocusRating = val;
            updateStarDisplay(val);
        });
    });

    // ================== TIMER LOGIC ==================
    function startTimer() {
        if (isPaused) {
            // Unpause
            isPaused = false;
            endTime = Date.now() + timeRemaining;
            startInterval();
            return;
        }
        // Start fresh
        workDuration = parseInt(document.getElementById("workDuration").value) || 30;
        restDuration = parseInt(document.getElementById("restDuration").value) || 5;

        if (timerInterval) clearInterval(timerInterval);

        if (isWorkSession) {
            endTime = Date.now() + (workDuration * 60_000);
        } else {
            endTime = Date.now() + (restDuration * 60_000);
        }
        startInterval();
    }

    function pauseTimer() {
        if (!timerInterval) return;
        isPaused = true;
        const diff = endTime - Date.now();
        timeRemaining = (diff > 0) ? diff : 0;
        clearInterval(timerInterval);
        timerInterval = null;
    }

    function resetTimer() {
        if (timerInterval) clearInterval(timerInterval);
        timerInterval = null;
        endTime = 0;
        timeRemaining = 0;
        isPaused = false;
        isWorkSession = true;
        pendingFocusRating = 0;
        sessionLogs = [];
        thirtyMinSessionCount = 0;

        updateDisplay(0);
        updateStarDisplay(0);
        sessionLabelEl.textContent = "Work Session";
        completedSessionsEl.textContent = "0";
        completedSessionsEl.style.width = "0%";
        completedSessionsEl.setAttribute("aria-valuenow", "0");
    }

    function startInterval() {
        timerInterval = setInterval(() => {
            if (!isPaused) {
                let diff = endTime - Date.now();
                if (diff <= 0) {
                    clearInterval(timerInterval);
                    diff = 0;

                    // session ended
                    let usedMin = isWorkSession ? workDuration : restDuration;
                    logSession(isWorkSession, usedMin);

                    if (isWorkSession && usedMin === 30) {
                        thirtyMinSessionCount++;
                        completedSessionsEl.textContent = thirtyMinSessionCount;
                        const progress = Math.min(thirtyMinSessionCount, 8);
                        completedSessionsEl.style.width = `${progress * 12.5}%`;
                        completedSessionsEl.setAttribute("aria-valuenow", progress.toString());
                    }

                    // auto-record if it's a finished work session
                    if (isWorkSession) {
                        recordSessions(true);
                    }

                    isWorkSession = !isWorkSession;
                    pendingFocusRating = 0;
                    updateStarDisplay(0);
                    sessionLabelEl.textContent = isWorkSession ? "Work Session" : "Break Session";
                    startTimer();
                } else {
                    updateDisplay(diff);
                }
            }
        }, 250);
    }

    function updateDisplay(ms) {
        if (ms < 0) ms = 0;
        let totalSec = Math.floor(ms / 1000);
        let min = Math.floor(totalSec / 60);
        let sec = totalSec % 60;

        let minStr = (min < 10 ? "0" : "") + min;
        let secStr = (sec < 10 ? "0" : "") + sec;
        document.getElementById("timer-display").textContent = `${minStr}:${secStr}`;
    }

    function logSession(isWork, duration) {
        const endTimeStamp = new Date();
        const msDuration = duration * 60_000;
        const startTimeStamp = new Date(endTimeStamp - msDuration);

        sessionLogs.push({
            startTime: startTimeStamp.toISOString(),
            endTime: endTimeStamp.toISOString(),
            durationMinutes: duration,
            sessionType: isWork ? "Work" : "Rest",
            focusRating: pendingFocusRating,
            sessionCategory: selectedCategory || ""
        });

        if (isWork && pendingFocusRating > 0) {
            document.getElementById("self-intensity").textContent = `${pendingFocusRating} / 5`;
        }
    }

    // ================== RECORDING ==================
    async function recordSessions(isAuto) {
        // If paused, finalize partial usage first
        if (isPaused) {
            // do partial usage logic
            clearInterval(timerInterval);
            timerInterval = null;

            let fullMs = isWorkSession ? (workDuration * 60_000) : (restDuration * 60_000);
            let diff = endTime - Date.now();
            if (diff < 0) diff = 0;
            let usedMs = fullMs - diff;
            if (usedMs < 0) usedMs = 0;

            let usedMin = Math.round(usedMs / 60_000);
            // log partial usage
            logSession(isWorkSession, usedMin);
            // *Do not* resetTimer() yet, so sessionLogs isn't cleared
        }

        // If still no logs, just exit quietly (no popup)
        if (sessionLogs.length === 0) {
            return;
        }

        try {
            const resp = await fetch("/Timer/RecordSession", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(sessionLogs)
            });
            if (resp.ok) {
                if (!isAuto) {
                    alert("Sessions recorded successfully.");
                }
                // Now that they've been recorded, we can clear
                sessionLogs = [];
                // Now reset the timer state
                resetTimer();

                // Refresh improvement data + chart
                await fetchVolumeIncrease();
                await loadDailyTotalsChart();
            } else {
                const err = await resp.text();
                if (!isAuto) {
                    alert("Error recording sessions: " + err);
                }
            }
        } catch (error) {
            if (!isAuto) {
                alert("Error sending request: " + error);
            }
        }
    }

    function saveFocusRating() {
        if (pendingFocusRating > 0) {
            document.getElementById("self-intensity").textContent =
                `${pendingFocusRating} / 5 (pending)`;
            alert("Focus rating saved for the next Work session.");
        }
    }

    function updateStarDisplay(rating) {
        stars.forEach(star => {
            const val = parseInt(star.getAttribute("data-value"));
            star.classList.toggle("text-warning", val <= rating);
            star.classList.toggle("text-muted", val > rating);
        });
    }

    // ============== CHART & IMPROVEMENT ==============
    async function loadDailyTotalsChart() {
        try {
            const resp = await fetch("/Timer/GetDailyTotals");
            if (!resp.ok) return;
            const data = await resp.json();
            // data is e.g. [ { date: '2025-05-24T00:00:00', totalMinutes: 30 }, ... ] for 7 days

            const labels = data.map(d => d.date.substring(0, 10));
            const values = data.map(d => d.totalMinutes);

            const ctx = document.getElementById('myChart').getContext('2d');
            new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Daily Work (min)',
                        data: values,
                        backgroundColor: 'rgba(75, 192, 192, 0.6)'
                    }]
                },
                options: {
                    scales: {
                        y: { beginAtZero: true }
                    }
                }
            });
        }
        catch (err) {
            console.error("Error loading chart data:", err);
        }
    }

    async function fetchVolumeIncrease() {
        try {
            const resp = await fetch("/Timer/GetDailyImprovement");
            if (!resp.ok) return;
            const data = await resp.json();

            document.getElementById("improvement-percentage").textContent =
                `${data.improvementPercent.toFixed(1)}%`;

            document.getElementById("today-job-volume").textContent =
                data.todayJobVolume;
            document.getElementById("prev-job-volume").textContent =
                data.previousDayJobVolume;
            document.getElementById("today-personal-volume").textContent =
                data.todayPersonalVolume;
            document.getElementById("prev-personal-volume").textContent =
                data.previousDayPersonalVolume;

        } catch (err) {
            console.error("Failed to fetch daily improvement:", err);
        }
    }
