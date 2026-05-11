document.addEventListener("DOMContentLoaded", function () {

    const tabs = document.querySelectorAll(".balance-tabs .tab");
    const rows = document.querySelectorAll(".order-table tbody tr");

    tabs.forEach(tab => {

        tab.addEventListener("click", function () {

            // Remove active state
            tabs.forEach(t => t.classList.remove("active"));
            this.classList.add("active");

            const filterType = this.dataset.type;

            rows.forEach(row => {

                const rowType = row.dataset.type;

                if (filterType === "all") {
                    row.style.display = "";
                }
                else if (rowType && rowType.includes(filterType)) {
                    row.style.display = "";
                }
                else {
                    row.style.display = "none";
                }

            });

        });

    });

});