let timer;
let timeLeft = 30;
let score = 0;
let answered = false;

async function loadQuestion() {
    clearInterval(timer);
    timeLeft = 30;
    answered = false;

    // Reset timer UI
    const timeBox = document.getElementById("timeBox");
    timeBox.textContent = timeLeft;
    timeBox.style.color = "white";
    timeBox.classList.remove("pulse");

    const difficulty = document.getElementById("difficulty").value;
    const res = await fetch(`/api/questions/random/${difficulty}`);
    const question = await res.json();

    document.getElementById("question").textContent = question.questionText;

    const qImg = document.getElementById("questionImage");
    qImg.innerHTML = question.imageUrl ? `<img src="${question.imageUrl}" alt="Question Image" style="max-height:200px;">` : "";

    const optionsDiv = document.getElementById("options");
    optionsDiv.innerHTML = "";

    question.options.forEach(opt => {
        const btn = document.createElement("div");
        btn.classList.add("option");
        if (opt.imageUrl) {
            btn.innerHTML = `<img src="${opt.imageUrl}" alt="option">`;
        } else {
            btn.textContent = opt.text;
        }

        btn.onclick = () => {
            if (answered) return;
            answered = true;
            clearInterval(timer);

            if (opt.isCorrect) {
                btn.classList.add("correct");
                score++;
                updateScore();
            } else {
                btn.classList.add("wrong");
                // highlight correct one
                document.querySelectorAll(".option").forEach(o => {
                    if (o.textContent === opt.text && opt.isCorrect) {
                        o.classList.add("correct");
                    }
                });
                highlightCorrectAnswer(question);
            }

            disableOptions();
        };

        optionsDiv.appendChild(btn);
    });

    startTimer(question);
}

function startTimer(question) {
    const timeBox = document.getElementById("timeBox");
    timer = setInterval(() => {
        timeLeft--;
        timeBox.textContent = timeLeft;

        if (timeLeft <= 10 && timeLeft > 5) {
            timeBox.style.color = "red";
        }

        if (timeLeft <= 5) {
            timeBox.classList.add("pulse");
        }

        if (timeLeft <= 0) {
            clearInterval(timer);
            if (!answered) {
                highlightCorrectAnswer(question);
                disableOptions();
            }
        }
    }, 1000);
}

function disableOptions() {
    document.querySelectorAll(".option").forEach(o => o.classList.add("disabled"));
}

function highlightCorrectAnswer(question) {
    const optionsDiv = document.getElementById("options");
    optionsDiv.querySelectorAll(".option").forEach((o, idx) => {
        if (question.options[idx].isCorrect) {
            o.classList.add("correct");
        }
    });
}

function updateScore() {
    document.getElementById("score").textContent = score;
    document.getElementById("sideScore").textContent = score;
}

// Disable right-click
document.addEventListener("contextmenu", e => e.preventDefault());
