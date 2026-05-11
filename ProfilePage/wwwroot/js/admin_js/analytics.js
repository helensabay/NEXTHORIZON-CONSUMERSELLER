let salesTrendChart; // Move to global scope so it's accessible by the update function

document.addEventListener('DOMContentLoaded', function () {
    const ctx = document.getElementById('salesTrendChart').getContext('2d');


    salesTrendChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: ['Apr 1', 'Apr 6', 'Apr 13', 'Apr 15', 'Apr 21', 'Apr 30'],
            datasets: [{
                label: 'GMV',
                data: [25000, 30000, 23000, 31000, 45000, 56000],
                borderColor: '#000',
                backgroundColor: 'rgba(0,0,0,0.05)',
                fill: true,
                tension: 0.3,
                pointRadius: 4,
                pointBackgroundColor: '#000'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            // Adds the Peso sign to the chart tooltips
                            return 'GMV: ₱' + context.parsed.y.toLocaleString();
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: { color: '#f0f0f0' },
                    ticks: {
                        font: { size: 10 },
                        callback: function (value) { return '₱' + (value / 1000) + 'k'; }
                    }
                },
                x: {
                    grid: { display: false },
                    ticks: { font: { size: 10 } }
                }
            }
        }
    });
});

document.addEventListener('DOMContentLoaded', function () {
    const statusCtx = document.getElementById('orderStatusChart').getContext('2d');

    new Chart(statusCtx, {
        type: 'bar',
        data: {
            labels: ['Pending', 'Processing', 'Shipped', 'Delivered', 'Refunded', 'Cancelled'],
            datasets: [{
                data: [1000, 600, 1200, 650, 900, 980],
                backgroundColor: [
                    '#444444', // Pending
                    '#666666', // Processing
                    '#000000', // Shipped (Highlight)
                    '#888888', // Delivered
                    '#aaaaaa', // Refunded
                    '#cccccc'  // Cancelled
                ],
                borderRadius: 4,
                barThickness: 20
            }]
        },
        options: {
            indexAxis: 'y', // Makes the bar graph horizontal
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    enabled: true,
                    backgroundColor: '#000',
                    titleFont: { size: 12 },
                    bodyFont: { size: 12 }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    grid: { display: false },
                    ticks: { display: false } // Hides bottom numbers for a cleaner look
                },
                y: {
                    grid: { display: false },
                    ticks: {
                        font: { size: 11, weight: 'bold' },
                        color: '#333'
                    }
                }
            }
        }
    });
});

// Function to handle the Dropdown Filter updates
function updateTrendFilter(label, element) {
    // 1. UI Updates
    document.getElementById('trendFilterDropdown').innerText = label;
    document.querySelectorAll('.dropdown-item').forEach(item => item.classList.remove('active'));
    element.classList.add('active');

    // 2. Data Update Logic
    // In a real app, you would fetch this data from your Controller
    let newData = [];
    let newLabels = [];

    if (label === 'Daily') {
        newLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
        newData = [5000, 7000, 4000, 8000, 12000, 15000, 9000];
    } else if (label === 'Monthly') {
        newLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'];
        newData = [150000, 230000, 180000, 345000, 290000, 410000];
    } else if (label.includes('Yearly')) {
        newLabels = ['Q1', 'Q2', 'Q3', 'Q4'];
        newData = [800000, 1200000, 950000, 1500000];
    } else {
        // Default to Weekly (initial state)
        newLabels = ['Apr 1', 'Apr 6', 'Apr 13', 'Apr 15', 'Apr 21', 'Apr 30'];
        newData = [25000, 30000, 23000, 31000, 45000, 56000];
    }

    // 3. Apply to Chart
    salesTrendChart.data.labels = newLabels;
    salesTrendChart.data.datasets[0].data = newData;
    salesTrendChart.update();
}

function switchTab(viewName) {
    const views = ['view-seller', 'view-orders', 'view-products'];
    const tabs = ['tab-seller', 'tab-orders', 'tab-products'];

    views.forEach(v => document.getElementById(v).classList.add('d-none'));

    tabs.forEach(t => {
        const tabBtn = document.getElementById(t);
        // Remove active styles including the new colors
        tabBtn.classList.remove('fw-bold', 'bg-dark', 'text-white', 'active-tab');
        tabBtn.classList.add('text-muted');
        tabBtn.style.padding = "5px 15px"; // Maintain consistent sizing
    });

    document.getElementById('view-' + viewName).classList.remove('d-none');

    const activeBtn = document.getElementById('tab-' + viewName);
    // Add black background and white text
    activeBtn.classList.add('fw-bold', 'bg-dark', 'text-white', 'active-tab');
    activeBtn.classList.remove('text-muted');
}