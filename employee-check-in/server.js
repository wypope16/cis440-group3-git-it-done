const express = require('express');
const mysql = require('mysql2');
const cors = require('cors');

const app = express();

app.use(express.json());
app.use(cors());

// serve the needed files from this folder
app.use(express.static('.'));

// connect to the database
const db = mysql.createConnection({
  host: '107.180.1.16',
  port: 3306,
  user: 'cis440sum26team3',
  password: 'cis440sum26team3',
  database: 'cis440sum26team3'
});

db.connect((err) => {
  if (err) {
    console.error('Database connection failed:', err.message);
  } else {
    console.log('Connected successfully to MySQL Database!');
    
    const createTableQuery = `
      CREATE TABLE IF NOT EXISTS check_ins (
        id INT AUTO_INCREMENT PRIMARY KEY,
        feedback TEXT NOT NULL,
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
      )
    `;
    
    db.query(createTableQuery, (err, result) => {
      if (err) {
        console.error('Error creating table:', err.message);
      } else {
        console.log('Table "check_ins" is ready to go!');
      }
    });
  }
});

// handles the check-in form submission
app.post('/api/checkin', (req, res) => {
  const { feedback } = req.body;

// insert the feedback into check_ins
  const sql = 'INSERT INTO check_ins (feedback) VALUES (?)';
  
  db.query(sql, [feedback], (err, result) => {
    if (err) {
  console.error('MySQL Insert Error:', err);
  // send back a friendly error, keep whatever they typed on the frontend
  return res.status(500).json({ success: false, message: 'Database error' });
}

    // just a plain success msg, no need to send back the row id or anything
return res.json({ success: true, message: 'Check-in successfully recorded' });
  });
});

// start server on port 3000
app.listen(3000, () => {
  console.log('Server is running on http://localhost:3000');
});
