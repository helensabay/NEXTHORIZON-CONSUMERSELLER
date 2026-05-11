document.addEventListener("DOMContentLoaded", function() {
    const tableRows = document.querySelectorAll(".orders-table tbody tr");

    tableRows.forEach(row => {
        row.addEventListener("click", function() {
            // Example: highlight clicked row
            tableRows.forEach(r => r.classList.remove("selected"));
            this.classList.add("selected");

            // Optional: show alert with transaction reference
            const ref = this.querySelector("td:first-child").textContent;
            console.log("Selected transaction:", ref);
        });
    });
});